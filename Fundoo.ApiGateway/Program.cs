namespace Fundoo.ApiGateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

            var app = builder.Build();

            app.MapGet("/health", () => Results.Ok("Healthy"));
            app.MapReverseProxy();

            app.Run();
        }
    }
}
