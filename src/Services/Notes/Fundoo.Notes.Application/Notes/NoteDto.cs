using Fundoo.Notes.Application.Labels;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Notes;

public record NoteDto(
    Guid Id, string Title, string Content, string? Color,
    bool IsPinned, bool IsArchived, bool IsTrashed,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    List<LabelDto> Labels)
{
    public static NoteDto From(Note n) => new(
        n.Id, n.Title, n.Content, n.Color,
        n.IsPinned, n.IsArchived, n.IsTrashed,
        n.CreatedAtUtc, n.UpdatedAtUtc,
        n.NoteLabels.Select(nl => new LabelDto(nl.Label.Id, nl.Label.Name)).ToList());
}

public enum NoteOperationStatus
{
    Success,
    NotFound,
    ForbiddenReadOnly,
    NotOwner,
    NotTrashed
}

public record NoteOperationResult<T>(NoteOperationStatus Status, T? Data = default, string? Message = null)
{
    public static NoteOperationResult<T> Ok(T data) => new(NoteOperationStatus.Success, data);
    public static NoteOperationResult<T> NotFound() => new(NoteOperationStatus.NotFound, Message: "Note not found.");
    public static NoteOperationResult<T> Forbidden(string msg) => new(NoteOperationStatus.ForbiddenReadOnly, Message: msg);
    public static NoteOperationResult<T> NotOwner() => new(NoteOperationStatus.NotOwner, Message: "Only the note owner can perform this action.");
}