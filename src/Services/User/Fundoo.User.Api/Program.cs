using System.Text;
using Fundoo.User.Infrastructure;
using Asp.Versioning;
using FluentValidation;
using Fundoo.User.Api.Filters;
using Fundoo.User.Api.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Configure NLog for logging
builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddUserInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

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
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Fundoo User API", Version = "v1" });
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
            message = "An unexpected error occurred in User Service.",
            detail = app.Environment.IsDevelopment() ? ex?.Message : null
        });
    });
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fundoo User API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<Fundoo.User.Infrastructure.Persistence.UserDbContext>();
        db.Database.EnsureCreated();
    }
    catch
    {
        // Skip if DB is not reachable at startup
    }
}

app.Run();
