using Fundoo.Notes.Application.Collaborators;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Abstractions;

public interface ICollaboratorRepository
{
    Task<List<NoteCollaborator>> GetCollaboratorsByNoteIdAsync(Guid noteId, CancellationToken ct = default);
    Task<NoteCollaborator?> GetCollaboratorAsync(Guid noteId, Guid userId, CancellationToken ct = default);
    Task AddCollaboratorAsync(NoteCollaborator collaborator, CancellationToken ct = default);
    Task UpdateCollaboratorAsync(NoteCollaborator collaborator, CancellationToken ct = default);
    Task DeleteCollaboratorAsync(Guid noteId, Guid userId, CancellationToken ct = default);
    Task AddHistoryAsync(NoteShareHistory history, CancellationToken ct = default);
    Task<List<NoteShareHistory>> GetHistoryByNoteIdAsync(Guid noteId, CancellationToken ct = default);
    Task<List<Guid>> GetSharedNoteIdsForUserAsync(Guid userId, CancellationToken ct = default);
}
