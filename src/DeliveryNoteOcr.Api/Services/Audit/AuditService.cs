using System.Text.Json;
using DeliveryNoteOcr.Api.Data.Entities;
using DeliveryNoteOcr.Api.Domain;

namespace DeliveryNoteOcr.Api.Services.Audit;

public class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public AuditEvent Build<T>(
        Guid entityId,
        AuditAction action,
        T? before,
        T? after,
        string actorUserId,
        AuditSource source = AuditSource.Manual,
        string? reasonCode = null) where T : class
    {
        var diff = BuildFieldDiff(before, after);
        return new AuditEvent
        {
            EntityType = typeof(T).Name,
            EntityId = entityId,
            Action = action,
            Source = source,
            ActorUserId = actorUserId,
            ReasonCode = reasonCode,
            DiffJson = diff is null ? null : JsonSerializer.Serialize(diff, JsonOpts)
        };
    }

    public AuditEvent BuildSimple(
        string entityType,
        Guid entityId,
        AuditAction action,
        string actorUserId,
        AuditSource source = AuditSource.System,
        string? reasonCode = null,
        object? payload = null)
    {
        return new AuditEvent
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Source = source,
            ActorUserId = actorUserId,
            ReasonCode = reasonCode,
            DiffJson = payload is null ? null : JsonSerializer.Serialize(payload, JsonOpts)
        };
    }

    private static Dictionary<string, object>? BuildFieldDiff<T>(T? before, T? after) where T : class
    {
        if (before is null && after is null) return null;

        var diff = new Dictionary<string, object>();
        var props = typeof(T).GetProperties();
        foreach (var p in props)
        {
            if (!p.CanRead) continue;
            if (p.PropertyType.IsClass && p.PropertyType != typeof(string)) continue;

            var b = before is null ? null : p.GetValue(before);
            var a = after is null ? null : p.GetValue(after);
            if (!Equals(b, a))
                diff[p.Name] = new { before = b, after = a };
        }
        return diff.Count == 0 ? null : diff;
    }
}
