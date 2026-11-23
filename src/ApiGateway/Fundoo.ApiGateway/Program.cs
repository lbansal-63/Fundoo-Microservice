using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Polly;
using Ocelot.Provider.Consul;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Configure NLog for logging
builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var ocelotFile = builder.Configuration["OcelotConfigFile"] ?? "ocelot.json";
builder.Configuration.AddJsonFile(ocelotFile, optional: false, reloadOnChange: true);
builder.Services.AddOcelot(builder.Configuration).AddPolly().AddConsul();

// Unified Swagger UI on API Gateway
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Har request par chalta hai, Ocelot se pehle
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isAuth = context.Request.Path.Value?.Contains("/auth/", StringComparison.OrdinalIgnoreCase) == true;

        // Har IP ke liye alag counter, aur auth ke liye alag, sakht limit
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{(isAuth ? "auth" : "api")}:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isAuth ? 10 : 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Too many requests. Please try again later.\"}", token);
    };
});

var app = builder.Build();

app.UseForwardedHeaders();

// /health par limit nahi, isliye limiter se pehle
app.Map("/health", health => health.Run(ctx => ctx.Response.WriteAsync("Healthy")));

// Unified Swagger UI - allows testing all microservices from single gateway page
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/docs/user/v1/swagger.json", "User Service API (v1)");
    c.SwaggerEndpoint("/swagger/docs/notes/v1/swagger.json", "Notes Service API (v1)");
    c.SwaggerEndpoint("/swagger/docs/notes/v2/swagger.json", "Notes Service API (v2)");
    c.RoutePrefix = "swagger";
});

app.UseRateLimiter();
await app.UseOcelot();
app.Run();
