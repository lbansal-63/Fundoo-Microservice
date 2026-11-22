using System.Text.Json;
using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Notes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Fundoo.Notes.Infrastructure.Caching;

/// <summary>
/// Redis-based distributed caching implementation for Notes.
/// Agar Redis offline ho ya error aaye toh gracefully bypass karta hai taaki app crash na ho.
/// </summary>
public class RedisNoteCacheService(
    IDistributedCache cache,
    ILogger<RedisNoteCacheService> logger) : INoteCacheService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(10);

    private static string GetCacheKey(Guid userId, NoteView view) =>
        $"fundoo:notes:{userId}:{view}";

    public async Task<List<NoteDto>?> GetNotesAsync(Guid userId, NoteView view, CancellationToken ct = default)
    {
        try
        {
            var key = GetCacheKey(userId, view);
            var cachedData = await cache.GetStringAsync(key, ct);
            if (string.IsNullOrEmpty(cachedData))
                return null;

            logger.LogDebug("Cache HIT for key: {CacheKey}", key);
            return JsonSerializer.Deserialize<List<NoteDto>>(cachedData);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Redis cache read failed for user {UserId}: {Message}", userId, ex.Message);
            return null;
        }
    }

    public async Task SetNotesAsync(Guid userId, NoteView view, List<NoteDto> notes, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        try
        {
            var key = GetCacheKey(userId, view);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? DefaultExpiry
            };

            var jsonData = JsonSerializer.Serialize(notes);
            await cache.SetStringAsync(key, jsonData, options, ct);
            logger.LogDebug("Cache SET for key: {CacheKey}", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Redis cache write failed for user {UserId}: {Message}", userId, ex.Message);
        }
    }

    public async Task RemoveUserNotesCacheAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            // Note change hone par user ke sabhi views (Active, Archived, Trashed, All) ka cache invalidate karo
            var views = Enum.GetValues<NoteView>();
            foreach (var view in views)
            {
                var key = GetCacheKey(userId, view);
                await cache.RemoveAsync(key, ct);
            }
            logger.LogDebug("Cache invalidated for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Redis cache invalidate failed for user {UserId}: {Message}", userId, ex.Message);
        }
    }
}
