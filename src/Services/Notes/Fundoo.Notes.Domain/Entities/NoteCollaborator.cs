namespace Fundoo.Notes.Domain.Entities;

public class NoteCollaborator
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public CollaboratorPermission Permission { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
