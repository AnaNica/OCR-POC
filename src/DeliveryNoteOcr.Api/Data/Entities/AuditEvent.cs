using DeliveryNoteOcr.Api.Domain;

namespace DeliveryNoteOcr.Api.Data.Entities;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public AuditSource Source { get; set; } = AuditSource.Manual;
    public string ActorUserId { get; set; } = "system";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DiffJson { get; set; }
    public string? ReasonCode { get; set; }
}
