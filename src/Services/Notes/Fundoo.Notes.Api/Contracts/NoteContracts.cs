namespace Fundoo.Notes.Api.Contracts;

public class CreateNoteRequest
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? Color { get; set; }
}

public class UpdateNoteRequest
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? Color { get; set; }
    public bool IsPinned { get; set; }
}

// NAYA: v2 ki paging wali list ka response
public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);