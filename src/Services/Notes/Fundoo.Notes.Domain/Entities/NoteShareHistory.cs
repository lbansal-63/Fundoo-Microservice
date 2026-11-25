namespace Fundoo.Notes.Domain.Entities;

public enum ShareAction
{
    Added = 1,
    PermissionChanged = 2,
    Removed = 3
}

public class NoteShareHistory
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public Guid PerformedByUserId { get; set; }
    public Guid TargetUserId { get; set; }
    public string TargetUserEmail { get; set; } = string.Empty;
    public ShareAction Action { get; set; }
    public CollaboratorPermission? OldPermission { get; set; }
    public CollaboratorPermission? NewPermission { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
