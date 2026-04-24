using DeliveryNoteOcr.Api.Data;
using DeliveryNoteOcr.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DeliveryNoteOcr.Api.Services.Training;

public interface IRetrainCoordinator
{
    Task<RetrainStatus> GetStatusAsync(CancellationToken ct);
    Task<TrainingRun> TriggerAsync(string actorUserId, string? notes, CancellationToken ct);
}

public record RetrainStatus(int PendingLabelCount, int Threshold, bool EligibleForAutoRetrain,
    TrainingRun? LatestRun);

public class RetrainCoordinator : IRetrainCoordinator
{
    private readonly AppDbContext _db;
    private readonly TrainingOptions _options;
    private readonly ILogger<RetrainCoordinator> _logger;

    public RetrainCoordinator(
        AppDbContext db,
        IOptions<TrainingOptions> options,
        ILogger<RetrainCoordinator> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RetrainStatus> GetStatusAsync(CancellationToken ct)
    {
        var pending = await _db.TrainingLabels
            .CountAsync(l => l.TrainingRunId == null, ct);
        var latest = await _db.TrainingRuns
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);
        return new RetrainStatus(
            pending,
            _options.AutoRetrainThreshold,
            pending >= _options.AutoRetrainThreshold,
            latest);
    }

    public async Task<TrainingRun> TriggerAsync(string actorUserId, string? notes, CancellationToken ct)
    {
        var pending = await _db.TrainingLabels
            .Where(l => l.TrainingRunId == null)
            .ToListAsync(ct);

        var run = new TrainingRun
        {
            Status = "Queued",
            LabelCount = pending.Count,
            Notes = notes
        };
        _db.TrainingRuns.Add(run);

        foreach (var l in pending)
            l.TrainingRunId = run.Id;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Queued training run {RunId} with {Count} labels (triggered by {Actor})",
            run.Id, pending.Count, actorUserId);

        return run;
    }
}
