namespace DeliveryNoteOcr.Api.Data.Entities;

public class CorrectionCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ContentHash { get; set; } = string.Empty;

    public string? DeliveryNoteNo { get; set; }
    public string? ProjectNumber { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public Guid? AssigneeCompanyId { get; set; }
    public string? AssigneeRawText { get; set; }
    public string? SupplierName { get; set; }
    public string? Site { get; set; }
    public string? CostCentre { get; set; }

    public Guid SourceNoteId { get; set; }
    public int TimesApplied { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedBy { get; set; } = "system";
}
