namespace DeliveryNoteOcr.Api.Data.Entities;

public class TrainingLabel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeliveryNoteId { get; set; }
    public DeliveryNote? DeliveryNote { get; set; }

    public string BlobPath { get; set; } = string.Empty;
    public string FinalLabelsJson { get; set; } = string.Empty;
    public string? OriginalExtractionJson { get; set; }

    public string ModelIdUsedAtExtraction { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "system";

    public Guid? TrainingRunId { get; set; }
    public TrainingRun? TrainingRun { get; set; }
}

public class TrainingRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = "Pending";
    public string? BaseModelId { get; set; }
    public string? ResultModelId { get; set; }
    public int LabelCount { get; set; }
    public string? EvaluationJson { get; set; }
    public bool Promoted { get; set; }
    public string? Notes { get; set; }
}
