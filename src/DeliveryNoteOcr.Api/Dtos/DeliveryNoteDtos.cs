using DeliveryNoteOcr.Api.Data.Entities;
using DeliveryNoteOcr.Api.Domain;

namespace DeliveryNoteOcr.Api.Dtos;

public record DeliveryNoteListItemDto(
    Guid Id,
    string OriginalFileName,
    string? DeliveryNoteNo,
    string? ProjectNumber,
    DateOnly? DeliveryDate,
    string? AssigneeName,
    DeliveryNoteStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record DeliveryNoteDetailDto(
    Guid Id,
    string OriginalFileName,
    string BlobPath,
    string? DeliveryNoteNo,
    string? ProjectNumber,
    DateOnly? DeliveryDate,
    Guid? AssigneeCompanyId,
    string? AssigneeCompanyName,
    string? AssigneeRawText,
    string? SupplierName,
    string? Site,
    string? CostCentre,
    Dictionary<string, float?> FieldConfidences,
    string? ModelIdUsed,
    DeliveryNoteStatus Status,
    string? ExtractionError,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedBy);

public record UpdateDeliveryNoteDto(
    string? DeliveryNoteNo,
    string? ProjectNumber,
    DateOnly? DeliveryDate,
    Guid? AssigneeCompanyId,
    string? AssigneeRawText,
    string? SupplierName,
    string? Site,
    string? CostCentre);

public record ConfirmDeliveryNoteDto(string? ReasonCode);

public static class DeliveryNoteMapper
{
    public static DeliveryNoteListItemDto ToListItem(DeliveryNote n) => new(
        n.Id, n.OriginalFileName, n.DeliveryNoteNo, n.ProjectNumber, n.DeliveryDate,
        n.AssigneeCompany?.Name ?? n.AssigneeRawText,
        n.Status, n.CreatedAt, n.UpdatedAt);

    public static DeliveryNoteDetailDto ToDetail(DeliveryNote n)
    {
        var confidences = new Dictionary<string, float?>();
        if (!string.IsNullOrWhiteSpace(n.FieldConfidencesJson))
        {
            try
            {
                var parsed = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, float?>>(n.FieldConfidencesJson);
                if (parsed is not null) confidences = parsed;
            }
            catch { /* tolerate malformed json */ }
        }

        return new DeliveryNoteDetailDto(
            n.Id, n.OriginalFileName, n.BlobPath,
            n.DeliveryNoteNo, n.ProjectNumber, n.DeliveryDate,
            n.AssigneeCompanyId, n.AssigneeCompany?.Name, n.AssigneeRawText,
            n.SupplierName, n.Site, n.CostCentre,
            confidences, n.ModelIdUsed,
            n.Status, n.ExtractionError,
            n.CreatedAt, n.CreatedBy,
            n.UpdatedAt, n.UpdatedBy,
            n.ConfirmedAt, n.ConfirmedBy);
    }
}
