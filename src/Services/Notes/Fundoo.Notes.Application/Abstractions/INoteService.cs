using Fundoo.Notes.Application.Notes;

namespace Fundoo.Notes.Application.Abstractions;

public interface INoteService
{
    Task<NoteDto> CreateAsync(Guid userId, string title, string content, string? color, CancellationToken ct = default);
    Task<List<NoteDto>> ListAsync(Guid userId, NoteView view, CancellationToken ct = default);
    Task<NoteOperationResult<NoteDto>> GetAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<NoteOperationResult<NoteDto>> UpdateAsync(Guid userId, Guid id, string title, string content, string? color, bool isPinned, CancellationToken ct = default);
    Task<NoteOperationResult<NoteDto>> SetArchivedAsync(Guid userId, Guid id, bool archived, CancellationToken ct = default);
    Task<NoteOperationResult<NoteDto>> SetTrashedAsync(Guid userId, Guid id, bool trashed, CancellationToken ct = default);
    Task<NoteOperationResult<bool>> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}