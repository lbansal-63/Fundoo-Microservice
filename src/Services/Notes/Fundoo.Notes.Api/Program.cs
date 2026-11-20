using System.Text;
using Fundoo.Notes.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Asp.Versioning;
using FluentValidation;
using Fundoo.Notes.Api.Filters;
using Fundoo.Notes.Api.Validators;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Configure NLog for logging
builder.Logging.ClearProviders();
builder.Host.UseNLog();

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

builder.Services.AddNotesInfrastructure(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("NotesDb")
    ?? throw new InvalidOperationException("ConnectionStrings:NotesDb is missing.");

builder.Services.AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: new[] { "live" })
    .AddNpgSql(
        connectionString,
        name: "postgresql",
        tags: new[] { "ready" },
        timeout: TimeSpan.FromSeconds(3));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers(o => o.Filters.Add<FluentValidationFilter>()); 
builder.Services.AddValidatorsFromAssemblyContaining<CreateNoteRequestValidator>();
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = false;
    o.ReportApiVersions = true;
    o.ApiVersionReader = new UrlSegmentApiVersionReader();
})
    .AddMvc()
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Fundoo Notes API", Version = "v1" });
    c.SwaggerDoc("v2", new OpenApiInfo { Title = "Fundoo Notes API", Version = "v2" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Global Exception Handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var exFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var ex = exFeature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        if (ex != null)
        {
            logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
        }
        await context.Response.WriteAsJsonAsync(new
        {
            statusCode = 500,
            message = "An unexpected error occurred in Notes Service.",
            detail = app.Environment.IsDevelopment() ? ex?.Message : null
        });
    });
});

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    o.SwaggerEndpoint("/swagger/v2/swagger.json", "v2");
    o.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapGet("/health", () => Results.Ok("Healthy"));

await app.Services.EnsureNotesTablesCreatedAsync();

app.Run();
