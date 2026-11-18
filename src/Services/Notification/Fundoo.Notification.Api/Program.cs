using Fundoo.Notification.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using FluentValidation;
using Fundoo.Notification.Api.Filters;
using Fundoo.Notification.Api.Validators;

var builder = WebApplication.CreateBuilder(args);

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
            ctx.User.FindFirst("scope")?.Value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("notification.send") == true));
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));
app.Run();