using Microsoft.Extensions.Options;

namespace DeliveryNoteOcr.Api.Services.Storage;

public class LocalFileStorageOptions
{
    public string RootPath { get; set; } = "data/blobs";
}

public class LocalFileStorage : IDocumentStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<LocalFileStorageOptions> options, IHostEnvironment env)
    {
        _root = Path.IsPathRooted(options.Value.RootPath)
            ? options.Value.RootPath
            : Path.Combine(env.ContentRootPath, options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        Guid deliveryNoteId, string fileName, Stream content, CancellationToken ct)
    {
        var safeName = string.Concat(fileName.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var relative = Path.Combine(
            deliveryNoteId.ToString("N")[..2],
            $"{deliveryNoteId:N}_{safeName}");
        var absolute = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        await using var target = File.Create(absolute);
        await content.CopyToAsync(target, ct);

        return relative.Replace('\\', '/');
    }

    public Task<Stream> OpenAsync(string blobPath, CancellationToken ct)
    {
        var absolute = Resolve(blobPath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Blob not found", blobPath);
        Stream s = File.OpenRead(absolute);
        return Task.FromResult(s);
    }

    public bool Exists(string blobPath) => File.Exists(Resolve(blobPath));

    private string Resolve(string blobPath)
    {
        var normalized = blobPath.Replace('/', Path.DirectorySeparatorChar);
        var absolute = Path.GetFullPath(Path.Combine(_root, normalized));
        if (!absolute.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid blob path.");
        return absolute;
    }
}
