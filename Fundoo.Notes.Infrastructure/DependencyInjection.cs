using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Labels;
using Fundoo.Notes.Application.Notes;
using Fundoo.Notes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fundoo.Notes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("NotesDb")
            ?? throw new InvalidOperationException("ConnectionStrings:NotesDb is missing.");

        services.AddDbContext<NotesDbContext>(o =>
            o.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(NotesDbContext).Assembly.FullName)));

        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<ILabelService, LabelService>();
        return services;
    }
}