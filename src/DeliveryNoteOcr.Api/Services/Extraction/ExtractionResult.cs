namespace DeliveryNoteOcr.Api.Services.Extraction;

public record ExtractedField(string? Value, float? Confidence);

public class ExtractionResult
{
    public ExtractedField DeliveryNoteNo { get; set; } = new(null, null);
    public ExtractedField ProjectNumber { get; set; } = new(null, null);
    public ExtractedField DeliveryDate { get; set; } = new(null, null);
    public ExtractedField Assignee { get; set; } = new(null, null);
    public ExtractedField SupplierName { get; set; } = new(null, null);
    public ExtractedField Site { get; set; } = new(null, null);
    public ExtractedField CostCentre { get; set; } = new(null, null);

    public string ModelIdUsed { get; set; } = string.Empty;
    public string RawResponseJson { get; set; } = "{}";

    public Dictionary<string, float?> AsConfidenceMap() => new()
    {
        ["deliveryNoteNo"] = DeliveryNoteNo.Confidence,
        ["projectNumber"] = ProjectNumber.Confidence,
        ["deliveryDate"] = DeliveryDate.Confidence,
        ["assignee"] = Assignee.Confidence,
        ["supplierName"] = SupplierName.Confidence,
        ["site"] = Site.Confidence,
        ["costCentre"] = CostCentre.Confidence,
    };
}
