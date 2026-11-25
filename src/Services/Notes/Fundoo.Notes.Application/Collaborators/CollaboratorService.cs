using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Collaborators;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Collaborators;

public class CollaboratorService(
    ICollaboratorRepository collaboratorRepo,
    INoteRepository noteRepo,
    INoteCacheService? cache = null) : ICollaboratorService
{
    public async Task<(CollaboratorOperationResult Result, CollaboratorDto? Collaborator)> AddCollaboratorAsync(
        Guid ownerId, Guid noteId, Guid targetUserId, string targetUserEmail, CollaboratorPermission permission, CancellationToken ct = default)
    {
        var note = await noteRepo.GetByIdAsync(noteId, ct);
        if (note is null)
            return (CollaboratorOperationResult.NoteNotFound, null);

        if (note.UserId != ownerId)
            return (CollaboratorOperationResult.NotOwner, null);

        if (targetUserId == ownerId)
            return (CollaboratorOperationResult.CannotCollaborateWithOwner, null);

        var existing = await collaboratorRepo.GetCollaboratorAsync(noteId, targetUserId, ct);
        if (existing != null)
            return (CollaboratorOperationResult.AlreadyCollaborator, null);

        var now = DateTime.UtcNow;
        var collaborator = new NoteCollaborator
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            UserId = targetUserId,
            UserEmail = targetUserEmail.Trim().ToLowerInvariant(),
            Permission = permission,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await collaboratorRepo.AddCollaboratorAsync(collaborator, ct);

        // Add to history
        var history = new NoteShareHistory
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            PerformedByUserId = ownerId,
            TargetUserId = targetUserId,
            TargetUserEmail = targetUserEmail,
            Action = ShareAction.Added,
            OldPermission = null,
            NewPermission = permission,
            CreatedAtUtc = now
        };
        await collaboratorRepo.AddHistoryAsync(history, ct);

        if (cache != null)
        {
            await cache.RemoveUserNotesCacheAsync(ownerId, ct);
            await cache.RemoveUserNotesCacheAsync(targetUserId, ct);
        }

        return (CollaboratorOperationResult.Success, CollaboratorDto.From(collaborator));
    }

    public async Task<(CollaboratorOperationResult Result, CollaboratorDto? Collaborator)> UpdatePermissionAsync(
        Guid ownerId, Guid noteId, Guid collaboratorUserId, CollaboratorPermission newPermission, CancellationToken ct = default)
    {
        var note = await noteRepo.GetByIdAsync(noteId, ct);
        if (note is null)
            return (CollaboratorOperationResult.NoteNotFound, null);

        if (note.UserId != ownerId)
            return (CollaboratorOperationResult.NotOwner, null);

        var collaborator = await collaboratorRepo.GetCollaboratorAsync(noteId, collaboratorUserId, ct);
        if (collaborator is null)
            return (CollaboratorOperationResult.CollaboratorNotFound, null);

        var oldPermission = collaborator.Permission;
        if (oldPermission == newPermission)
            return (CollaboratorOperationResult.Success, CollaboratorDto.From(collaborator));

        collaborator.Permission = newPermission;
        collaborator.UpdatedAtUtc = DateTime.UtcNow;
        await collaboratorRepo.UpdateCollaboratorAsync(collaborator, ct);

        // Add to history
        var history = new NoteShareHistory
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            PerformedByUserId = ownerId,
            TargetUserId = collaboratorUserId,
            TargetUserEmail = collaborator.UserEmail,
            Action = ShareAction.PermissionChanged,
            OldPermission = oldPermission,
            NewPermission = newPermission,
            CreatedAtUtc = DateTime.UtcNow
        };
        await collaboratorRepo.AddHistoryAsync(history, ct);

        if (cache != null)
        {
            await cache.RemoveUserNotesCacheAsync(ownerId, ct);
            await cache.RemoveUserNotesCacheAsync(collaboratorUserId, ct);
        }

        return (CollaboratorOperationResult.Success, CollaboratorDto.From(collaborator));
    }

    public async Task<CollaboratorOperationResult> RemoveCollaboratorAsync(
        Guid ownerId, Guid noteId, Guid collaboratorUserId, CancellationToken ct = default)
    {
        var note = await noteRepo.GetByIdAsync(noteId, ct);
        if (note is null)
            return CollaboratorOperationResult.NoteNotFound;

        if (note.UserId != ownerId)
            return CollaboratorOperationResult.NotOwner;

        var collaborator = await collaboratorRepo.GetCollaboratorAsync(noteId, collaboratorUserId, ct);
        if (collaborator is null)
            return CollaboratorOperationResult.CollaboratorNotFound;

        await collaboratorRepo.DeleteCollaboratorAsync(noteId, collaboratorUserId, ct);

        // Add to history
        var history = new NoteShareHistory
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            PerformedByUserId = ownerId,
            TargetUserId = collaboratorUserId,
            TargetUserEmail = collaborator.UserEmail,
            Action = ShareAction.Removed,
            OldPermission = collaborator.Permission,
            NewPermission = null,
            CreatedAtUtc = DateTime.UtcNow
        };
        await collaboratorRepo.AddHistoryAsync(history, ct);

        if (cache != null)
        {
            await cache.RemoveUserNotesCacheAsync(ownerId, ct);
            await cache.RemoveUserNotesCacheAsync(collaboratorUserId, ct);
        }

        return CollaboratorOperationResult.Success;
    }

    public async Task<(CollaboratorOperationResult Result, List<CollaboratorDto> Collaborators)> GetCollaboratorsAsync(
        Guid requestingUserId, Guid noteId, CancellationToken ct = default)
    {
        var note = await noteRepo.GetByIdAsync(noteId, ct);
        if (note is null)
            return (CollaboratorOperationResult.NoteNotFound, new());

        // Only owner or existing collaborators can view collaborator list
        var isOwner = note.UserId == requestingUserId;
        var collabs = await collaboratorRepo.GetCollaboratorsByNoteIdAsync(noteId, ct);
        var isCollaborator = collabs.Any(c => c.UserId == requestingUserId);

        if (!isOwner && !isCollaborator)
            return (CollaboratorOperationResult.NotOwner, new());

        return (CollaboratorOperationResult.Success, collabs.Select(CollaboratorDto.From).ToList());
    }

    public async Task<(CollaboratorOperationResult Result, List<ShareHistoryDto> History)> GetHistoryAsync(
        Guid ownerId, Guid noteId, CancellationToken ct = default)
    {
        var note = await noteRepo.GetByIdAsync(noteId, ct);
        if (note is null)
            return (CollaboratorOperationResult.NoteNotFound, new());

        if (note.UserId != ownerId)
            return (CollaboratorOperationResult.NotOwner, new());

        var history = await collaboratorRepo.GetHistoryByNoteIdAsync(noteId, ct);
        return (CollaboratorOperationResult.Success, history.Select(ShareHistoryDto.From).ToList());
    }

    public async Task<CollaboratorPermission?> GetUserPermissionAsync(Guid noteId, Guid userId, CancellationToken ct = default)
    {
        var c = await collaboratorRepo.GetCollaboratorAsync(noteId, userId, ct);
        return c?.Permission;
    }
}
