using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.UnitTests;

public class FakeCollaboratorRepository : ICollaboratorRepository
{
    public List<NoteCollaborator> Collaborators { get; } = new();
    public List<NoteShareHistory> History { get; } = new();

    public Task<List<NoteCollaborator>> GetCollaboratorsByNoteIdAsync(Guid noteId, CancellationToken ct = default) =>
        Task.FromResult(Collaborators.Where(c => c.NoteId == noteId).ToList());

    public Task<NoteCollaborator?> GetCollaboratorAsync(Guid noteId, Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Collaborators.FirstOrDefault(c => c.NoteId == noteId && c.UserId == userId));

    public Task AddCollaboratorAsync(NoteCollaborator collaborator, CancellationToken ct = default)
    {
        Collaborators.Add(collaborator);
        return Task.CompletedTask;
    }

    public Task UpdateCollaboratorAsync(NoteCollaborator collaborator, CancellationToken ct = default)
    {
        var existing = Collaborators.FirstOrDefault(c => c.NoteId == collaborator.NoteId && c.UserId == collaborator.UserId);
        if (existing != null)
        {
            existing.Permission = collaborator.Permission;
            existing.UpdatedAtUtc = collaborator.UpdatedAtUtc;
        }
        return Task.CompletedTask;
    }

    public Task DeleteCollaboratorAsync(Guid noteId, Guid userId, CancellationToken ct = default)
    {
        Collaborators.RemoveAll(c => c.NoteId == noteId && c.UserId == userId);
        return Task.CompletedTask;
    }

    public Task AddHistoryAsync(NoteShareHistory history, CancellationToken ct = default)
    {
        History.Add(history);
        return Task.CompletedTask;
    }

    public Task<List<NoteShareHistory>> GetHistoryByNoteIdAsync(Guid noteId, CancellationToken ct = default) =>
        Task.FromResult(History.Where(h => h.NoteId == noteId).OrderByDescending(h => h.CreatedAtUtc).ToList());

    public Task<List<Guid>> GetSharedNoteIdsForUserAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Collaborators.Where(c => c.UserId == userId).Select(c => c.NoteId).Distinct().ToList());
}
