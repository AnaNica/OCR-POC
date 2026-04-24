using DeliveryNoteOcr.Api.Data.Entities;
using DeliveryNoteOcr.Api.Domain;

namespace DeliveryNoteOcr.Api.Services.Audit;

public interface IAuditService
{
    AuditEvent Build<T>(
        Guid entityId,
        AuditAction action,
        T? before,
        T? after,
        string actorUserId,
        AuditSource source = AuditSource.Manual,
        string? reasonCode = null) where T : class;

    AuditEvent BuildSimple(
        string entityType,
        Guid entityId,
        AuditAction action,
        string actorUserId,
        AuditSource source = AuditSource.System,
        string? reasonCode = null,
        object? payload = null);
}
