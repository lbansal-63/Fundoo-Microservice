using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Labels;

public class LabelService(ILabelRepository labels, INoteRepository notes) : ILabelService
{
    public async Task<List<LabelDto>> ListAsync(Guid userId, CancellationToken ct = default) =>
        (await labels.ListAsync(userId, ct)).Select(LabelDto.From).ToList();

    public async Task<LabelOutcome> CreateAsync(Guid userId, string name, CancellationToken ct = default)
    {
        name = name.Trim();
        if (await labels.NameExistsAsync(userId, name, null, ct))
            return new LabelOutcome(LabelResult.Duplicate);

        var label = new Label { Id = Guid.NewGuid(), UserId = userId, Name = name };
        await labels.AddAsync(label, ct);
        await labels.SaveChangesAsync(ct);
        return new LabelOutcome(LabelResult.Ok, LabelDto.From(label));
    }

    public async Task<LabelOutcome> RenameAsync(Guid userId, Guid id, string name, CancellationToken ct = default)
    {
        var label = await labels.GetOwnedAsync(userId, id, ct);
        if (label is null) return new LabelOutcome(LabelResult.NotFound);

        name = name.Trim();
        if (await labels.NameExistsAsync(userId, name, id, ct))
            return new LabelOutcome(LabelResult.Duplicate);

        label.Name = name;
        await labels.SaveChangesAsync(ct);
        return new LabelOutcome(LabelResult.Ok, LabelDto.From(label));
    }

    public async Task<LabelResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var label = await labels.GetOwnedAsync(userId, id, ct);
        if (label is null) return LabelResult.NotFound;

        labels.Remove(label);               // NoteLabel links are removed by cascade
        await labels.SaveChangesAsync(ct);
        return LabelResult.Ok;
    }

    public async Task<LabelResult> AttachAsync(Guid userId, Guid noteId, Guid labelId, CancellationToken ct = default)
    {
        // BOTH must belong to the caller, otherwise it looks like they don't exist
        var note = await notes.GetOwnedAsync(userId, noteId, ct);
        var label = await labels.GetOwnedAsync(userId, labelId, ct);
        if (note is null || label is null) return LabelResult.NotFound;

        if (await labels.FindLinkAsync(noteId, labelId, ct) is null)
        {
            await labels.AddLinkAsync(new NoteLabel { NoteId = noteId, LabelId = labelId }, ct);
            await labels.SaveChangesAsync(ct);
        }
        return LabelResult.Ok;              // attaching twice is harmless
    }

    public async Task<LabelResult> DetachAsync(Guid userId, Guid noteId, Guid labelId, CancellationToken ct = default)
    {
        var note = await notes.GetOwnedAsync(userId, noteId, ct);
        var label = await labels.GetOwnedAsync(userId, labelId, ct);
        if (note is null || label is null) return LabelResult.NotFound;

        var link = await labels.FindLinkAsync(noteId, labelId, ct);
        if (link is not null)
        {
            labels.RemoveLink(link);
            await labels.SaveChangesAsync(ct);
        }
        return LabelResult.Ok;
    }
}