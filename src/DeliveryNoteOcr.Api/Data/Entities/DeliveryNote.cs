using DeliveryNoteOcr.Api.Domain;

namespace DeliveryNoteOcr.Api.Data.Entities;

public class DeliveryNote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? DeliveryNoteNo { get; set; }
    public string? ProjectNumber { get; set; }
    public DateOnly? DeliveryDate { get; set; }

    public Guid? AssigneeCompanyId { get; set; }
    public Company? AssigneeCompany { get; set; }
    public string? AssigneeRawText { get; set; }

    public string? SupplierName { get; set; }
    public string? Site { get; set; }
    public string? CostCentre { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentHash { get; set; } = string.Empty;

    public string? RawExtractedJson { get; set; }
    public string? FieldConfidencesJson { get; set; }
    public string? ModelIdUsed { get; set; }

    public DeliveryNoteStatus Status { get; set; } = DeliveryNoteStatus.Extracting;
    public string? ExtractionError { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedBy { get; set; } = "system";
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }

    public List<AuditEvent> AuditEvents { get; set; } = new();
}
