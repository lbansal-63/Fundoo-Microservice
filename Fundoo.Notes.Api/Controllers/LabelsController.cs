using Asp.Versioning;
using Fundoo.Notes.Api.Contracts;
using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Labels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}")]
public class LabelsController(ILabelService labels) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet("labels")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await labels.ListAsync(UserId, ct));

    [HttpPost("labels")]
    public async Task<IActionResult> Create(LabelRequest r, CancellationToken ct)
    {
        var outcome = await labels.CreateAsync(UserId, r.Name, ct);
        return outcome.Result switch
        {
            LabelResult.Duplicate => Problem(statusCode: StatusCodes.Status409Conflict, title: "A label with this name already exists."),
            _ => StatusCode(StatusCodes.Status201Created, outcome.Label)
        };
    }

    [HttpPut("labels/{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, LabelRequest r, CancellationToken ct)
    {
        var outcome = await labels.RenameAsync(UserId, id, r.Name, ct);
        return outcome.Result switch
        {
            LabelResult.NotFound => NotFound(),
            LabelResult.Duplicate => Problem(statusCode: StatusCodes.Status409Conflict, title: "A label with this name already exists."),
            _ => Ok(outcome.Label)
        };
    }

    [HttpDelete("labels/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await labels.DeleteAsync(UserId, id, ct) == LabelResult.NotFound ? NotFound() : NoContent();

    [HttpPut("notes/{noteId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> Attach(Guid noteId, Guid labelId, CancellationToken ct) =>
        await labels.AttachAsync(UserId, noteId, labelId, ct) == LabelResult.NotFound ? NotFound() : NoContent();

    [HttpDelete("notes/{noteId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> Detach(Guid noteId, Guid labelId, CancellationToken ct) =>
        await labels.DetachAsync(UserId, noteId, labelId, ct) == LabelResult.NotFound ? NotFound() : NoContent();
}