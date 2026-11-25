using Asp.Versioning;
using Fundoo.Notes.Api.Contracts;
using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Notes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/notes")]
public class NotesController(INoteService notes) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPost]
    public async Task<IActionResult> Create(CreateNoteRequest r, CancellationToken ct)
    {
        var note = await notes.CreateAsync(UserId, r.Title, r.Content, r.Color, ct);
        return CreatedAtAction(nameof(Get), new { id = note.Id }, note);
    }

    // v1: purana format, seedha list
    [HttpGet]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await notes.ListAsync(UserId, NoteView.Active, ct));

    // v2: paging ke saath object
    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<IActionResult> ListV2(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var all = await notes.ListAsync(UserId, NoteView.Active, ct);
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new PagedResponse<NoteDto>(items, page, pageSize, all.Count));
    }

    [HttpGet("archived")]
    public async Task<IActionResult> Archived(CancellationToken ct) =>
        Ok(await notes.ListAsync(UserId, NoteView.Archived, ct));

    [HttpGet("trash")]
    public async Task<IActionResult> Trash(CancellationToken ct) =>
        Ok(await notes.ListAsync(UserId, NoteView.Trashed, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await notes.GetAsync(UserId, id, ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateNoteRequest r, CancellationToken ct)
    {
        var result = await notes.UpdateAsync(UserId, id, r.Title, r.Content, r.Color, r.IsPinned, ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await notes.SetArchivedAsync(UserId, id, true, ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid id, CancellationToken ct)
    {
        var result = await notes.SetArchivedAsync(UserId, id, false, ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}/trash")]
    public async Task<IActionResult> MoveToTrash(Guid id, CancellationToken ct)
    {
        var result = await notes.SetTrashedAsync(UserId, id, true, ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var result = await notes.SetTrashedAsync(UserId, id, false, ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await notes.DeleteAsync(UserId, id, ct);
        return result.Status switch
        {
            NoteOperationStatus.Success => NoContent(),
            NoteOperationStatus.NotTrashed => Problem(statusCode: StatusCodes.Status409Conflict, title: result.Message ?? "Only trashed notes can be permanently deleted."),
            NoteOperationStatus.NotOwner => Problem(statusCode: StatusCodes.Status403Forbidden, title: result.Message ?? "Only the note owner can permanently delete this note."),
            _ => NotFound(new { title = "Note not found." })
        };
    }

    private IActionResult ToResult(NoteOperationResult<NoteDto> res) => res.Status switch
    {
        NoteOperationStatus.Success => Ok(res.Data),
        NoteOperationStatus.ForbiddenReadOnly => Problem(statusCode: StatusCodes.Status403Forbidden, title: res.Message),
        NoteOperationStatus.NotOwner => Problem(statusCode: StatusCodes.Status403Forbidden, title: res.Message),
        _ => NotFound(new { title = res.Message ?? "Note not found." })
    };
}
