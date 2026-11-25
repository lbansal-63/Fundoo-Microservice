using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Collaborators;

public record CollaboratorDto(
    Guid Id,
    Guid NoteId,
    Guid UserId,
    string UserEmail,
    string Permission,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static CollaboratorDto From(NoteCollaborator c) =>
        new(c.Id, c.NoteId, c.UserId, c.UserEmail, c.Permission.ToString(), c.CreatedAtUtc, c.UpdatedAtUtc);
}

public record ShareHistoryDto(
    Guid Id,
    Guid NoteId,
    Guid PerformedByUserId,
    Guid TargetUserId,
    string TargetUserEmail,
    string Action,
    string? OldPermission,
    string? NewPermission,
    DateTime CreatedAtUtc)
{
    public static ShareHistoryDto From(NoteShareHistory h) =>
        new(h.Id, h.NoteId, h.PerformedByUserId, h.TargetUserId, h.TargetUserEmail,
            h.Action.ToString(), h.OldPermission?.ToString(), h.NewPermission?.ToString(), h.CreatedAtUtc);
}

public enum CollaboratorOperationResult
{
    Success,
    NoteNotFound,
    NotOwner,
    AlreadyCollaborator,
    CollaboratorNotFound,
    CannotCollaborateWithOwner,
    InvalidPermission
}
