using Fundoo.User.Infrastructure;
using Asp.Versioning;
using FluentValidation;
using Fundoo.User.Api.Filters;
using Fundoo.User.Api.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUserInfrastructure(builder.Configuration);
builder.Services.AddControllers(o => o.Filters.Add<FluentValidationFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = false;     // URL mein version likhna zaroori
    o.ReportApiVersions = true;                        // response headers mein supported versions
    o.ApiVersionReader = new UrlSegmentApiVersionReader();
})
    .AddMvc()
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";                      // swagger ke liye group: v1
        o.SubstituteApiVersionInUrl = true;                // swagger mein {version} ki jagah asli version
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();