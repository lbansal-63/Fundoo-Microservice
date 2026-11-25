using Asp.Versioning;
using Fundoo.Notes.Api.Contracts;
using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Collaborators;
using Fundoo.Notes.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/{noteId:guid}/collaborators")]
public class NoteCollaboratorsController(ICollaboratorService collaboratorService) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPost]
    public async Task<IActionResult> AddCollaborator(Guid noteId, AddCollaboratorRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<CollaboratorPermission>(request.Permission, ignoreCase: true, out var permission))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid permission. Allowed values are 'Reader' or 'Editor'.");
        }

        var (result, collaborator) = await collaboratorService.AddCollaboratorAsync(
            UserId, noteId, request.TargetUserId, request.TargetUserEmail, permission, ct);

        return result switch
        {
            CollaboratorOperationResult.Success => Ok(collaborator),
            CollaboratorOperationResult.NoteNotFound => NotFound(new { title = "Note not found." }),
            CollaboratorOperationResult.NotOwner => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the note owner can manage collaborators."),
            CollaboratorOperationResult.CannotCollaborateWithOwner => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Owner cannot be added as collaborator."),
            CollaboratorOperationResult.AlreadyCollaborator => Problem(statusCode: StatusCodes.Status409Conflict, title: "User is already a collaborator on this note."),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Failed to add collaborator.")
        };
    }

    [HttpPut("{collaboratorUserId:guid}")]
    public async Task<IActionResult> UpdatePermission(
        Guid noteId, Guid collaboratorUserId, UpdateCollaboratorPermissionRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<CollaboratorPermission>(request.Permission, ignoreCase: true, out var permission))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid permission. Allowed values are 'Reader' or 'Editor'.");
        }

        var (result, collaborator) = await collaboratorService.UpdatePermissionAsync(
            UserId, noteId, collaboratorUserId, permission, ct);

        return result switch
        {
            CollaboratorOperationResult.Success => Ok(collaborator),
            CollaboratorOperationResult.NoteNotFound => NotFound(new { title = "Note not found." }),
            CollaboratorOperationResult.NotOwner => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the note owner can update collaborator permissions."),
            CollaboratorOperationResult.CollaboratorNotFound => NotFound(new { title = "Collaborator not found on this note." }),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Failed to update permission.")
        };
    }

    [HttpDelete("{collaboratorUserId:guid}")]
    public async Task<IActionResult> RemoveCollaborator(Guid noteId, Guid collaboratorUserId, CancellationToken ct)
    {
        var result = await collaboratorService.RemoveCollaboratorAsync(UserId, noteId, collaboratorUserId, ct);

        return result switch
        {
            CollaboratorOperationResult.Success => NoContent(),
            CollaboratorOperationResult.NoteNotFound => NotFound(new { title = "Note not found." }),
            CollaboratorOperationResult.NotOwner => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the note owner can remove collaborators."),
            CollaboratorOperationResult.CollaboratorNotFound => NotFound(new { title = "Collaborator not found on this note." }),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Failed to remove collaborator.")
        };
    }

    [HttpGet]
    public async Task<IActionResult> ListCollaborators(Guid noteId, CancellationToken ct)
    {
        var (result, list) = await collaboratorService.GetCollaboratorsAsync(UserId, noteId, ct);

        return result switch
        {
            CollaboratorOperationResult.Success => Ok(list),
            CollaboratorOperationResult.NoteNotFound => NotFound(new { title = "Note not found." }),
            CollaboratorOperationResult.NotOwner => Problem(statusCode: StatusCodes.Status403Forbidden, title: "You do not have access to this note."),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Failed to retrieve collaborators.")
        };
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetShareHistory(Guid noteId, CancellationToken ct)
    {
        var (result, history) = await collaboratorService.GetHistoryAsync(UserId, noteId, ct);

        return result switch
        {
            CollaboratorOperationResult.Success => Ok(history),
            CollaboratorOperationResult.NoteNotFound => NotFound(new { title = "Note not found." }),
            CollaboratorOperationResult.NotOwner => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the note owner can view sharing history."),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Failed to retrieve history.")
        };
    }
}
