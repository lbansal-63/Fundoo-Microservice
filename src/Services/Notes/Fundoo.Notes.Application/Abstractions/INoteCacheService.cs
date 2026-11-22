using Fundoo.Notes.Application.Notes;

namespace Fundoo.Notes.Application.Abstractions;

/// <summary>
/// Notes ke liye distributed caching abstraction (e.g. Redis).
/// </summary>
public interface INoteCacheService
{
    Task<List<NoteDto>?> GetNotesAsync(Guid userId, NoteView view, CancellationToken ct = default);
    Task SetNotesAsync(Guid userId, NoteView view, List<NoteDto> notes, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveUserNotesCacheAsync(Guid userId, CancellationToken ct = default);
}
