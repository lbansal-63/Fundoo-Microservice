using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.Notes.Infrastructure.Persistence;

public class LabelRepository(NotesDbContext db) : ILabelRepository
{
    public Task<List<Label>> ListAsync(Guid userId, CancellationToken ct = default) =>
        db.Labels.Where(l => l.UserId == userId)
                 .OrderBy(l => l.Name)
                 .AsNoTracking()
                 .ToListAsync(ct);

    public Task<Label?> GetOwnedAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        db.Labels.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId, ct);

    public Task<bool> NameExistsAsync(Guid userId, string name, Guid? excludeId, CancellationToken ct = default)
    {
        var lowered = name.ToLower();
        return db.Labels.AnyAsync(l => l.UserId == userId
                                       && l.Name.ToLower() == lowered
                                       && (excludeId == null || l.Id != excludeId), ct);
    }

    public async Task AddAsync(Label label, CancellationToken ct = default) =>
        await db.Labels.AddAsync(label, ct);

    public void Remove(Label label) => db.Labels.Remove(label);

    public Task<NoteLabel?> FindLinkAsync(Guid noteId, Guid labelId, CancellationToken ct = default) =>
        db.NoteLabels.FirstOrDefaultAsync(x => x.NoteId == noteId && x.LabelId == labelId, ct);

    public async Task AddLinkAsync(NoteLabel link, CancellationToken ct = default) =>
        await db.NoteLabels.AddAsync(link, ct);

    public void RemoveLink(NoteLabel link) => db.NoteLabels.Remove(link);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}