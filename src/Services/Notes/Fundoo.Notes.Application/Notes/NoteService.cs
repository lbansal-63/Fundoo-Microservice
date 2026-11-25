using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Notes;

public class NoteService(
    INoteRepository repo,
    ICollaboratorRepository? collaboratorRepo = null,
    INoteCacheService? cache = null) : INoteService
{
    public async Task<NoteDto> CreateAsync(Guid userId, string title, string content, string? color, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title.Trim(),
            Content = content,
            Color = color,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await repo.AddAsync(note, ct);

        if (cache != null)
            await cache.RemoveUserNotesCacheAsync(userId, ct);

        return NoteDto.From(note);
    }

    public async Task<List<NoteDto>> ListAsync(Guid userId, NoteView view, CancellationToken ct = default)
    {
        if (cache != null)
        {
            var cached = await cache.GetNotesAsync(userId, view, ct);
            if (cached != null)
                return cached;
        }

        var ownedNotes = await repo.ListAsync(userId, view, ct);

        // Also fetch notes shared with this user if view is Active
        var allNotes = new List<Note>(ownedNotes);
        if (view == NoteView.Active && collaboratorRepo != null)
        {
            var sharedNoteIds = await collaboratorRepo.GetSharedNoteIdsForUserAsync(userId, ct);
            if (sharedNoteIds.Count > 0)
            {
                var sharedNotes = await repo.ListByIdsAsync(sharedNoteIds, ct);
                // Avoid duplicates if user is owner and also in collaborators table
                var existingIds = new HashSet<Guid>(ownedNotes.Select(n => n.Id));
                foreach (var sn in sharedNotes)
                {
                    if (existingIds.Add(sn.Id))
                        allNotes.Add(sn);
                }
            }
        }

        var dtoList = allNotes.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.UpdatedAtUtc).Select(NoteDto.From).ToList();

        if (cache != null)
        {
            await cache.SetNotesAsync(userId, view, dtoList, ct: ct);
        }

        return dtoList;
    }

    public async Task<NoteOperationResult<NoteDto>> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var note = await repo.GetByIdAsync(id, ct);
        if (note is null)
            return NoteOperationResult<NoteDto>.NotFound();

        // Check ownership or collaborator access
        if (note.UserId == userId)
            return NoteOperationResult<NoteDto>.Ok(NoteDto.From(note));

        if (collaboratorRepo != null)
        {
            var collaborator = await collaboratorRepo.GetCollaboratorAsync(id, userId, ct);
            if (collaborator != null)
                return NoteOperationResult<NoteDto>.Ok(NoteDto.From(note));
        }

        return NoteOperationResult<NoteDto>.NotFound();
    }

    public async Task<NoteOperationResult<NoteDto>> UpdateAsync(
        Guid userId, Guid id, string title, string content, string? color, bool isPinned, CancellationToken ct = default)
    {
        var note = await repo.GetByIdAsync(id, ct);
        if (note is null)
            return NoteOperationResult<NoteDto>.NotFound();

        // If not owner, check if user is Editor collaborator
        if (note.UserId != userId)
        {
            if (collaboratorRepo is null)
                return NoteOperationResult<NoteDto>.NotFound();

            var collaborator = await collaboratorRepo.GetCollaboratorAsync(id, userId, ct);
            if (collaborator is null)
                return NoteOperationResult<NoteDto>.NotFound();

            if (collaborator.Permission != CollaboratorPermission.Editor)
                return NoteOperationResult<NoteDto>.Forbidden("You have read-only access to this note and cannot modify it.");
        }

        note.Title = title.Trim();
        note.Content = content;
        note.Color = color;
        note.IsPinned = isPinned;
        note.UpdatedAtUtc = DateTime.UtcNow;

        await repo.UpdateAsync(note, ct);

        if (cache != null)
        {
            await cache.RemoveUserNotesCacheAsync(note.UserId, ct);
            if (note.UserId != userId)
                await cache.RemoveUserNotesCacheAsync(userId, ct);
        }

        return NoteOperationResult<NoteDto>.Ok(NoteDto.From(note));
    }

    public async Task<NoteOperationResult<NoteDto>> SetArchivedAsync(Guid userId, Guid id, bool archived, CancellationToken ct = default)
    {
        var note = await repo.GetByIdAsync(id, ct);
        if (note is null)
            return NoteOperationResult<NoteDto>.NotFound();

        // Only owner can archive/unarchive
        if (note.UserId != userId)
            return NoteOperationResult<NoteDto>.NotOwner();

        note.IsArchived = archived;
        if (archived) note.IsPinned = false;
        note.UpdatedAtUtc = DateTime.UtcNow;

        await repo.UpdateAsync(note, ct);

        if (cache != null)
            await cache.RemoveUserNotesCacheAsync(userId, ct);

        return NoteOperationResult<NoteDto>.Ok(NoteDto.From(note));
    }

    public async Task<NoteOperationResult<NoteDto>> SetTrashedAsync(Guid userId, Guid id, bool trashed, CancellationToken ct = default)
    {
        var note = await repo.GetByIdAsync(id, ct);
        if (note is null)
            return NoteOperationResult<NoteDto>.NotFound();

        // Only owner can trash/restore
        if (note.UserId != userId)
            return NoteOperationResult<NoteDto>.NotOwner();

        note.IsTrashed = trashed;
        if (trashed) note.IsPinned = false;
        note.UpdatedAtUtc = DateTime.UtcNow;

        await repo.UpdateAsync(note, ct);

        if (cache != null)
            await cache.RemoveUserNotesCacheAsync(userId, ct);

        return NoteOperationResult<NoteDto>.Ok(NoteDto.From(note));
    }

    public async Task<NoteOperationResult<bool>> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var note = await repo.GetByIdAsync(id, ct);
        if (note is null)
            return NoteOperationResult<bool>.NotFound();

        // Only owner can permanently delete
        if (note.UserId != userId)
            return NoteOperationResult<bool>.NotOwner();

        if (!note.IsTrashed)
            return new NoteOperationResult<bool>(NoteOperationStatus.NotTrashed, false, "Only trashed notes can be permanently deleted.");

        await repo.DeleteAsync(note, ct);

        if (cache != null)
            await cache.RemoveUserNotesCacheAsync(userId, ct);

        return NoteOperationResult<bool>.Ok(true);
    }
}
