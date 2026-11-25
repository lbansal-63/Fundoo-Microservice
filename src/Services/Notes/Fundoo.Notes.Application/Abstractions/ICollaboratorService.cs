using Fundoo.Notes.Application.Collaborators;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Abstractions;

public interface ICollaboratorService
{
    Task<(CollaboratorOperationResult Result, CollaboratorDto? Collaborator)> AddCollaboratorAsync(
        Guid ownerId, Guid noteId, Guid targetUserId, string targetUserEmail, CollaboratorPermission permission, CancellationToken ct = default);

    Task<(CollaboratorOperationResult Result, CollaboratorDto? Collaborator)> UpdatePermissionAsync(
        Guid ownerId, Guid noteId, Guid collaboratorUserId, CollaboratorPermission newPermission, CancellationToken ct = default);

    Task<CollaboratorOperationResult> RemoveCollaboratorAsync(
        Guid ownerId, Guid noteId, Guid collaboratorUserId, CancellationToken ct = default);

    Task<(CollaboratorOperationResult Result, List<CollaboratorDto> Collaborators)> GetCollaboratorsAsync(
        Guid requestingUserId, Guid noteId, CancellationToken ct = default);

    Task<(CollaboratorOperationResult Result, List<ShareHistoryDto> History)> GetHistoryAsync(
        Guid ownerId, Guid noteId, CancellationToken ct = default);

    Task<CollaboratorPermission?> GetUserPermissionAsync(Guid noteId, Guid userId, CancellationToken ct = default);
}
