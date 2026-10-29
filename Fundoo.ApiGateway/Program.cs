//Ocelot ka rate limit
//using Ocelot.DependencyInjection;
//using Ocelot.Middleware;
//using System.Threading.RateLimiting;

//var builder = WebApplication.CreateBuilder(args);

//builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
//builder.Services.AddOcelot(builder.Configuration);

//var app = builder.Build();

//app.Map("/health", health => health.Run(ctx => ctx.Response.WriteAsync("Healthy")));

//await app.UseOcelot();
//app.Run();



// ASP.NET Core ka limiter
using System.Threading.RateLimiting;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddOcelot(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Har request par chalta hai, Ocelot se pehle
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isAuth = context.Request.Path.StartsWithSegments("/api/auth");

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

// /health par limit nahi, isliye limiter se pehle
app.Map("/health", health => health.Run(ctx => ctx.Response.WriteAsync("Healthy")));

app.UseRateLimiter();
await app.UseOcelot();
app.Run();

