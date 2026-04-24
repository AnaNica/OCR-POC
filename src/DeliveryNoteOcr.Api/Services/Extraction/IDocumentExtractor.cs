namespace DeliveryNoteOcr.Api.Services.Extraction;

public interface IDocumentExtractor
{
    Task<ExtractionResult> ExtractAsync(Stream pdfStream, string originalFileName, CancellationToken ct);
}
