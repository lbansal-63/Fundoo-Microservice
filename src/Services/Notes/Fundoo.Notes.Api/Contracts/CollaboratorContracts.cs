using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Api.Contracts;

public class AddCollaboratorRequest
{
    public Guid TargetUserId { get; set; }
    public string TargetUserEmail { get; set; } = string.Empty;
    public string Permission { get; set; } = "Reader"; // "Reader" ya "Editor"
}

public class UpdateCollaboratorPermissionRequest
{
    public string Permission { get; set; } = "Reader"; // "Reader" ya "Editor"
}
