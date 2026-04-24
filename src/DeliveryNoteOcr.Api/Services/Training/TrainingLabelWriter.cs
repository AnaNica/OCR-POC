using System.Text.Json;
using DeliveryNoteOcr.Api.Data;
using DeliveryNoteOcr.Api.Data.Entities;
using Microsoft.Extensions.Options;

namespace DeliveryNoteOcr.Api.Services.Training;

public class TrainingLabelWriter : ITrainingLabelWriter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppDbContext _db;
    private readonly string _storeRoot;
    private readonly ILogger<TrainingLabelWriter> _logger;

    public TrainingLabelWriter(
        AppDbContext db,
        IOptions<TrainingOptions> options,
        IHostEnvironment env,
        ILogger<TrainingLabelWriter> logger)
    {
        _db = db;
        _logger = logger;
        var configured = options.Value.StorePath;
        _storeRoot = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
        Directory.CreateDirectory(_storeRoot);
    }

    public async Task<TrainingLabel> WriteAsync(DeliveryNote note, CancellationToken ct)
    {
        var finalLabels = new Dictionary<string, object?>
        {
            ["delivery_note_no"] = note.DeliveryNoteNo,
            ["project_number"] = note.ProjectNumber,
            ["delivery_date"] = note.DeliveryDate?.ToString("yyyy-MM-dd"),
            ["assignee"] = note.AssigneeCompany?.Name ?? note.AssigneeRawText,
            ["supplier_name"] = note.SupplierName,
            ["site"] = note.Site,
            ["cost_centre"] = note.CostCentre
        };

        var finalLabelsJson = JsonSerializer.Serialize(finalLabels, JsonOpts);

        var label = new TrainingLabel
        {
            DeliveryNoteId = note.Id,
            BlobPath = note.BlobPath,
            FinalLabelsJson = finalLabelsJson,
            OriginalExtractionJson = note.RawExtractedJson,
            ModelIdUsedAtExtraction = note.ModelIdUsed ?? string.Empty
        };
        _db.TrainingLabels.Add(label);

        await WriteTrainingArtifactsAsync(label, note, finalLabels, ct);
        return label;
    }

    private async Task WriteTrainingArtifactsAsync(
        TrainingLabel label,
        DeliveryNote note,
        Dictionary<string, object?> finalLabels,
        CancellationToken ct)
    {
        var dir = Path.Combine(_storeRoot, label.Id.ToString("N"));
        Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(
            Path.Combine(dir, "labels.json"),
            JsonSerializer.Serialize(new
            {
                deliveryNoteId = note.Id,
                blobPath = note.BlobPath,
                fields = finalLabels
            }, JsonOpts), ct);

        if (!string.IsNullOrWhiteSpace(note.RawExtractedJson))
            await File.WriteAllTextAsync(
                Path.Combine(dir, "ocr.json"), note.RawExtractedJson, ct);

        _logger.LogInformation(
            "Wrote training label {LabelId} for delivery note {NoteId} to {Dir}",
            label.Id, note.Id, dir);
    }
}
