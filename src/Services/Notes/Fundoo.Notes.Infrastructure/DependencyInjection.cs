using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Labels;
using Fundoo.Notes.Application.Notes;
using Fundoo.Notes.Infrastructure.Caching;
using Fundoo.Notes.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fundoo.Notes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("NotesDb")
            ?? throw new InvalidOperationException("ConnectionStrings:NotesDb is missing.");

        // One data source for the whole app (Npgsql handles connection pooling itself)
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        // Redis Distributed Caching configuration
        var redisConn = config.GetConnectionString("Redis")
            ?? config["Redis:Configuration"]
            ?? "localhost:6379";

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConn;
            options.InstanceName = "Fundoo_";
        });

        services.AddScoped<INoteCacheService, RedisNoteCacheService>();

        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<ILabelService, LabelService>();
        return services;
    }

    public static async Task EnsureNotesTablesCreatedAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        try
        {
            using var scope = services.CreateScope();
            var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
            await using var cmd = dataSource.CreateCommand("""
                CREATE TABLE IF NOT EXISTS notes (
                    id UUID PRIMARY KEY,
                    user_id UUID NOT NULL,
                    title VARCHAR(255) NOT NULL,
                    content TEXT NOT NULL,
                    color VARCHAR(50),
                    is_pinned BOOLEAN NOT NULL DEFAULT FALSE,
                    is_archived BOOLEAN NOT NULL DEFAULT FALSE,
                    is_trashed BOOLEAN NOT NULL DEFAULT FALSE,
                    created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
                    updated_at_utc TIMESTAMP WITH TIME ZONE NOT NULL
                );

                CREATE TABLE IF NOT EXISTS labels (
                    id UUID PRIMARY KEY,
                    user_id UUID NOT NULL,
                    name VARCHAR(100) NOT NULL
                );

                CREATE TABLE IF NOT EXISTS note_labels (
                    note_id UUID NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
                    label_id UUID NOT NULL REFERENCES labels(id) ON DELETE CASCADE,
                    PRIMARY KEY (note_id, label_id)
                );
                """);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Logging or skip if DB is not reachable at startup
        }
    }
}
