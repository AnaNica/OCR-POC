namespace DeliveryNoteOcr.Api.Services.Storage;

public interface IDocumentStorage
{
    Task<string> SaveAsync(Guid deliveryNoteId, string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenAsync(string blobPath, CancellationToken ct);
    bool Exists(string blobPath);
}
