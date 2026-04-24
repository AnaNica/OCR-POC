using DeliveryNoteOcr.Api.Data.Entities;

namespace DeliveryNoteOcr.Api.Services.Training;

public interface ITrainingLabelWriter
{
    Task<TrainingLabel> WriteAsync(DeliveryNote note, CancellationToken ct);
}
