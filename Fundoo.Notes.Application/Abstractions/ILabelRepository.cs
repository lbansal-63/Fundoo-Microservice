using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Abstractions;

public interface ILabelRepository
{
    Task<List<Label>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<Label?> GetOwnedAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(Guid userId, string name, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Label label, CancellationToken ct = default);
    void Remove(Label label);

    Task<NoteLabel?> FindLinkAsync(Guid noteId, Guid labelId, CancellationToken ct = default);
    Task AddLinkAsync(NoteLabel link, CancellationToken ct = default);
    void RemoveLink(NoteLabel link);

    Task SaveChangesAsync(CancellationToken ct = default);
}