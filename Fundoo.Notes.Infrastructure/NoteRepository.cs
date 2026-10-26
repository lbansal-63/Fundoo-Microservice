using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.Notes.Infrastructure.Persistence;

public class NoteRepository(NotesDbContext db) : INoteRepository
{
    public Task<Note?> GetOwnedAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        db.Notes
          .Include(n => n.NoteLabels).ThenInclude(nl => nl.Label)
          .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);

    public Task<List<Note>> ListAsync(Guid userId, NoteView view, CancellationToken ct = default)
    {
        var query = db.Notes
            .Include(n => n.NoteLabels).ThenInclude(nl => nl.Label)
            .Where(n => n.UserId == userId);

        query = view switch
        {
            NoteView.Active => query.Where(n => !n.IsArchived && !n.IsTrashed),
            NoteView.Archived => query.Where(n => n.IsArchived && !n.IsTrashed),
            NoteView.Trashed => query.Where(n => n.IsTrashed),
            _ => query
        };

        return query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AddAsync(Note note, CancellationToken ct = default) =>
        await db.Notes.AddAsync(note, ct);

    public void Remove(Note note) => db.Notes.Remove(note);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}