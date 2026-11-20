using Fundoo.Notification.Infrastructure;
using Fundoo.Notification.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi.Models;
using FluentValidation;
using Fundoo.Notification.Api.Filters;
using Fundoo.Notification.Api.Validators;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Configure NLog for logging
builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddNotificationInfrastructure(builder.Configuration);

var auth = builder.Configuration.GetSection("Auth");
var authority = auth["Authority"];
var audience = auth["Audience"];
if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
    throw new InvalidOperationException("Auth:Authority and Auth:Audience are required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = authority;
        o.Audience = audience;
        o.RequireHttpsMetadata = auth.GetValue("RequireHttpsMetadata", true);
        o.MapInboundClaims = false;
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("notification.send", p => p
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx =>
            ctx.User.Claims
                .Where(c => c.Type == "scope" || c.Type == "scp")
                .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains("notification.send")));
});

builder.Services.AddControllers(o => o.Filters.Add<FluentValidationFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<SendEmailRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
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
            message = "An unexpected error occurred in Notification Service.",
            detail = app.Environment.IsDevelopment() ? ex?.Message : null
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

// Auto-create database tables if DB is accessible
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        db.Database.EnsureCreated();
    }
    catch
    {
        // Skip if DB is not reachable at startup
    }
}

app.Run();
