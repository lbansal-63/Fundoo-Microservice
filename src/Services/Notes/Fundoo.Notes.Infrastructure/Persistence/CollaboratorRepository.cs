using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;
using Npgsql;

namespace Fundoo.Notes.Infrastructure.Persistence;

public class CollaboratorRepository(NpgsqlDataSource dataSource) : ICollaboratorRepository
{
    private const string CollaboratorColumns =
        "c.id, c.note_id, c.user_id, c.user_email, c.permission, c.created_at_utc, c.updated_at_utc";

    private const string HistoryColumns =
        "h.id, h.note_id, h.performed_by_user_id, h.target_user_id, h.target_user_email, h.action, h.old_permission, h.new_permission, h.created_at_utc";

    public async Task<List<NoteCollaborator>> GetCollaboratorsByNoteIdAsync(Guid noteId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var list = new List<NoteCollaborator>();

        var sql = $"SELECT {CollaboratorColumns} FROM note_collaborators c WHERE c.note_id = @noteId ORDER BY c.created_at_utc ASC";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("noteId", noteId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadCollaborator(reader));
        }

        return list;
    }

    public async Task<NoteCollaborator?> GetCollaboratorAsync(Guid noteId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var sql = $"SELECT {CollaboratorColumns} FROM note_collaborators c WHERE c.note_id = @noteId AND c.user_id = @userId";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("noteId", noteId);
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadCollaborator(reader);
        }

        return null;
    }

    public async Task AddCollaboratorAsync(NoteCollaborator collaborator, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO note_collaborators (id, note_id, user_id, user_email, permission, created_at_utc, updated_at_utc)
            VALUES (@id, @noteId, @userId, @userEmail, @permission, @createdAt, @updatedAt)
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", collaborator.Id);
        cmd.Parameters.AddWithValue("noteId", collaborator.NoteId);
        cmd.Parameters.AddWithValue("userId", collaborator.UserId);
        cmd.Parameters.AddWithValue("userEmail", collaborator.UserEmail);
        cmd.Parameters.AddWithValue("permission", collaborator.Permission.ToString());
        cmd.Parameters.AddWithValue("createdAt", collaborator.CreatedAtUtc);
        cmd.Parameters.AddWithValue("updatedAt", collaborator.UpdatedAtUtc);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateCollaboratorAsync(NoteCollaborator collaborator, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE note_collaborators
            SET permission = @permission, updated_at_utc = @updatedAt
            WHERE note_id = @noteId AND user_id = @userId
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("permission", collaborator.Permission.ToString());
        cmd.Parameters.AddWithValue("updatedAt", collaborator.UpdatedAtUtc);
        cmd.Parameters.AddWithValue("noteId", collaborator.NoteId);
        cmd.Parameters.AddWithValue("userId", collaborator.UserId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteCollaboratorAsync(Guid noteId, Guid userId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM note_collaborators WHERE note_id = @noteId AND user_id = @userId";
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("noteId", noteId);
        cmd.Parameters.AddWithValue("userId", userId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AddHistoryAsync(NoteShareHistory history, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO note_share_history (id, note_id, performed_by_user_id, target_user_id, target_user_email, action, old_permission, new_permission, created_at_utc)
            VALUES (@id, @noteId, @performedBy, @targetUser, @targetEmail, @action, @oldPerm, @newPerm, @createdAt)
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", history.Id);
        cmd.Parameters.AddWithValue("noteId", history.NoteId);
        cmd.Parameters.AddWithValue("performedBy", history.PerformedByUserId);
        cmd.Parameters.AddWithValue("targetUser", history.TargetUserId);
        cmd.Parameters.AddWithValue("targetEmail", history.TargetUserEmail);
        cmd.Parameters.AddWithValue("action", history.Action.ToString());
        cmd.Parameters.AddWithValue("oldPerm", (object?)history.OldPermission?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("newPerm", (object?)history.NewPermission?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("createdAt", history.CreatedAtUtc);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<NoteShareHistory>> GetHistoryByNoteIdAsync(Guid noteId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var list = new List<NoteShareHistory>();

        var sql = $"SELECT {HistoryColumns} FROM note_share_history h WHERE h.note_id = @noteId ORDER BY h.created_at_utc DESC";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("noteId", noteId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadHistory(reader));
        }

        return list;
    }

    public async Task<List<Guid>> GetSharedNoteIdsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var list = new List<Guid>();

        const string sql = "SELECT note_id FROM note_collaborators WHERE user_id = @userId";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(reader.GetGuid(0));
        }

        return list;
    }

    private static NoteCollaborator ReadCollaborator(NpgsqlDataReader r) =>
        new()
        {
            Id = r.GetGuid(0),
            NoteId = r.GetGuid(1),
            UserId = r.GetGuid(2),
            UserEmail = r.GetString(3),
            Permission = Enum.Parse<CollaboratorPermission>(r.GetString(4), ignoreCase: true),
            CreatedAtUtc = r.GetDateTime(5),
            UpdatedAtUtc = r.GetDateTime(6)
        };

    private static NoteShareHistory ReadHistory(NpgsqlDataReader r) =>
        new()
        {
            Id = r.GetGuid(0),
            NoteId = r.GetGuid(1),
            PerformedByUserId = r.GetGuid(2),
            TargetUserId = r.GetGuid(3),
            TargetUserEmail = r.GetString(4),
            Action = Enum.Parse<ShareAction>(r.GetString(5), ignoreCase: true),
            OldPermission = r.IsDBNull(6) ? null : Enum.Parse<CollaboratorPermission>(r.GetString(6), ignoreCase: true),
            NewPermission = r.IsDBNull(7) ? null : Enum.Parse<CollaboratorPermission>(r.GetString(7), ignoreCase: true),
            CreatedAtUtc = r.GetDateTime(8)
        };
}
