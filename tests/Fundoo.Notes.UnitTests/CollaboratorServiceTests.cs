using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Collaborators;
using Fundoo.Notes.Application.Notes;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.UnitTests;

public class CollaboratorServiceTests
{
    private readonly FakeNoteRepository _noteRepo = new();
    private readonly FakeCollaboratorRepository _collabRepo = new();
    private readonly CollaboratorService _collabService;
    private readonly NoteService _noteService;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _collabReaderId = Guid.NewGuid();
    private readonly Guid _collabEditorId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    public CollaboratorServiceTests()
    {
        _collabService = new CollaboratorService(_collabRepo, _noteRepo);
        _noteService = new NoteService(_noteRepo, _collabRepo);
    }

    [Fact] // Expectation 1: Share note with Read-Only permission
    public async Task Expectation1_ShareNote_WithReadPermission()
    {
        var note = await _noteService.CreateAsync(_ownerId, "Meeting Info", "Discuss project roadmap", null);

        var (result, collab) = await _collabService.AddCollaboratorAsync(
            _ownerId, note.Id, _collabReaderId, "reader@fundoo.com", CollaboratorPermission.Reader);

        Assert.Equal(CollaboratorOperationResult.Success, result);
        Assert.NotNull(collab);
        Assert.Equal("Reader", collab.Permission);

        // Colleague can view note
        var viewResult = await _noteService.GetAsync(_collabReaderId, note.Id);
        Assert.Equal(NoteOperationStatus.Success, viewResult.Status);
        Assert.Equal("Meeting Info", viewResult.Data!.Title);
    }

    [Fact] // Expectation 2: Share note with Edit permission
    public async Task Expectation2_ShareNote_WithEditPermission()
    {
        var note = await _noteService.CreateAsync(_ownerId, "Planning Note", "Initial ideas", null);

        var (result, collab) = await _collabService.AddCollaboratorAsync(
            _ownerId, note.Id, _collabEditorId, "editor@fundoo.com", CollaboratorPermission.Editor);

        Assert.Equal(CollaboratorOperationResult.Success, result);
        Assert.Equal("Editor", collab!.Permission);

        // Editor can update note content
        var updateResult = await _noteService.UpdateAsync(
            _collabEditorId, note.Id, "Planning Note v2", "Updated ideas by teammate", null, false);

        Assert.Equal(NoteOperationStatus.Success, updateResult.Status);
        Assert.Equal("Planning Note v2", updateResult.Data!.Title);
        Assert.Equal("Updated ideas by teammate", updateResult.Data!.Content);
    }

    [Fact] // Expectation 3: Managing multiple collaborators on one note
    public async Task Expectation3_ManageMultipleCollaborators_OnOneNote()
    {
        var note = await _noteService.CreateAsync(_ownerId, "Team Project", "Details", null);

        await _collabService.AddCollaboratorAsync(_ownerId, note.Id, _collabReaderId, "reader@fundoo.com", CollaboratorPermission.Reader);
        await _collabService.AddCollaboratorAsync(_ownerId, note.Id, _collabEditorId, "editor@fundoo.com", CollaboratorPermission.Editor);

        var (res, collabs) = await _collabService.GetCollaboratorsAsync(_ownerId, note.Id);

        Assert.Equal(CollaboratorOperationResult.Success, res);
        Assert.Equal(2, collabs.Count);
        Assert.Contains(collabs, c => c.UserId == _collabReaderId && c.Permission == "Reader");
        Assert.Contains(collabs, c => c.UserId == _collabEditorId && c.Permission == "Editor");
    }

    [Fact] // Expectation 4: Changing a collaborator's access
    public async Task Expectation4_ChangeCollaboratorAccess_EnforcedImmediately()
    {
        var note = await _noteService.CreateAsync(_ownerId, "Note", "Content", null);

        // Initially Editor
        await _collabService.AddCollaboratorAsync(_ownerId, note.Id, _collabEditorId, "collab@fundoo.com", CollaboratorPermission.Editor);

        var edit1 = await _noteService.UpdateAsync(_collabEditorId, note.Id, "Note v2", "Edit 1", null, false);
        Assert.Equal(NoteOperationStatus.Success, edit1.Status);

        // Owner demotes to Reader
        var (updateRes, updatedCollab) = await _collabService.UpdatePermissionAsync(
            _ownerId, note.Id, _collabEditorId, CollaboratorPermission.Reader);

        Assert.Equal(CollaboratorOperationResult.Success, updateRes);
        Assert.Equal("Reader", updatedCollab!.Permission);

        // Attempting to edit now should be rejected
        var edit2 = await _noteService.UpdateAsync(_collabEditorId, note.Id, "Note v3", "Edit 2", null, false);
        Assert.Equal(NoteOperationStatus.ForbiddenReadOnly, edit2.Status);
    }

    [Fact] // Expectation 5: Removing a collaborator from a note
    public async Task Expectation5_RemoveCollaborator_LosesAccessImmediately()
    {
        var note = await _noteService.CreateAsync(_ownerId, "Secret Note", "Secret content", null);

        await _collabService.AddCollaboratorAsync(_ownerId, note.Id, _collabReaderId, "collab@fundoo.com", CollaboratorPermission.Reader);

        // Remove collaborator
        var removeRes = await _collabService.RemoveCollaboratorAsync(_ownerId, note.Id, _collabReaderId);
        Assert.Equal(CollaboratorOperationResult.Success, removeRes);

        // Former collaborator cannot get or update note
        var getRes = await _noteService.GetAsync(_collabReaderId, note.Id);
        Assert.Equal(NoteOperationStatus.NotFound, getRes.Status);

        var updateRes = await _noteService.UpdateAsync(_collabReaderId, note.Id, "Hacked", "Hack", null, false);
        Assert.Equal(NoteOperationStatus.NotFound, updateRes.Status);
    }

    [Fact] // Expectation 6: Collaborator attempting actions beyond permission
    public async Task Expectation6_ActionsBeyondAccess_ArePrevented()
    {
        var note = await _noteService.CreateAsync(_ownerId, "Confidential", "Body", null);

        await _collabService.AddCollaboratorAsync(_ownerId, note.Id, _collabReaderId, "reader@fundoo.com", CollaboratorPermission.Reader);
        await _collabService.AddCollaboratorAsync(_ownerId, note.Id, _collabEditorId, "editor@fundoo.com", CollaboratorPermission.Editor);

        // 1. Reader cannot update
        var rUpdate = await _noteService.UpdateAsync(_collabReaderId, note.Id, "Change", "Change", null, false);
        Assert.Equal(NoteOperationStatus.ForbiddenReadOnly, rUpdate.Status);

        // 2. Editor cannot trash or archive note (only owner can)
        var eTrash = await _noteService.SetTrashedAsync(_collabEditorId, note.Id, true);
        Assert.Equal(NoteOperationStatus.NotOwner, eTrash.Status);

        var eArchive = await _noteService.SetArchivedAsync(_collabEditorId, note.Id, true);
        Assert.Equal(NoteOperationStatus.NotOwner, eArchive.Status);

        // 3. Editor cannot delete note
        var eDelete = await _noteService.DeleteAsync(_collabEditorId, note.Id);
        Assert.Equal(NoteOperationStatus.NotOwner, eDelete.Status);

        // 4. Stranger has zero access
        var sGet = await _noteService.GetAsync(_strangerId, note.Id);
        Assert.Equal(NoteOperationStatus.NotFound, sGet.Status);
    }

    [Fact] // Expectation 7 & 8: Reviewing sharing history (audit trail)
    public async Task Expectation7And8_ReviewSharingHistory_AndSequenceOfEvents()
    {
        var note = await _noteService.CreateAsync(_ownerId, "Audited Note", "Body", null);

        // 1. Add collaborator
        await _collabService.AddCollaboratorAsync(_ownerId, note.Id, _collabEditorId, "collab@fundoo.com", CollaboratorPermission.Editor);

        // 2. Change permission
        await _collabService.UpdatePermissionAsync(_ownerId, note.Id, _collabEditorId, CollaboratorPermission.Reader);

        // 3. Remove collaborator
        await _collabService.RemoveCollaboratorAsync(_ownerId, note.Id, _collabEditorId);

        // Review history
        var (histRes, history) = await _collabService.GetHistoryAsync(_ownerId, note.Id);

        Assert.Equal(CollaboratorOperationResult.Success, histRes);
        Assert.Equal(3, history.Count);

        // Sequence of actions checked
        Assert.Contains(history, h => h.Action == "Added" && h.NewPermission == "Editor" && h.OldPermission == null);
        Assert.Contains(history, h => h.Action == "PermissionChanged" && h.OldPermission == "Editor" && h.NewPermission == "Reader");
        Assert.Contains(history, h => h.Action == "Removed" && h.OldPermission == "Reader" && h.NewPermission == null);
        Assert.All(history, h => Assert.Equal(_ownerId, h.PerformedByUserId));
    }

    [Fact] // Expectation 9: Shared notes appear in collaborator's list
    public async Task Expectation9_SharedNotes_AppearInCollaboratorList()
    {
        var note = await _noteService.CreateAsync(_ownerId, "Shared Note", "Body", null);
        await _collabService.AddCollaboratorAsync(_ownerId, note.Id, _collabReaderId, "reader@fundoo.com", CollaboratorPermission.Reader);

        var list = await _noteService.ListAsync(_collabReaderId, NoteView.Active);

        Assert.Single(list);
        Assert.Equal("Shared Note", list[0].Title);
    }
}
