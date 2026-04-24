using System.Security.Cryptography;
using System.Text.Json;
using DeliveryNoteOcr.Api.Data;
using DeliveryNoteOcr.Api.Data.Entities;
using DeliveryNoteOcr.Api.Domain;
using DeliveryNoteOcr.Api.Dtos;
using DeliveryNoteOcr.Api.Services;
using DeliveryNoteOcr.Api.Services.Audit;
using DeliveryNoteOcr.Api.Services.Extraction;
using DeliveryNoteOcr.Api.Services.Storage;
using DeliveryNoteOcr.Api.Services.Training;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryNoteOcr.Api.Controllers;

[ApiController]
[Route("api/delivery-notes")]
public class DeliveryNotesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly IDocumentExtractor _extractor;
    private readonly ICompanyResolver _companies;
    private readonly IAuditService _audit;
    private readonly ITrainingLabelWriter _trainingWriter;
    private readonly ICurrentUser _user;
    private readonly ILogger<DeliveryNotesController> _logger;

    public DeliveryNotesController(
        AppDbContext db,
        IDocumentStorage storage,
        IDocumentExtractor extractor,
        ICompanyResolver companies,
        IAuditService audit,
        ITrainingLabelWriter trainingWriter,
        ICurrentUser user,
        ILogger<DeliveryNotesController> logger)
    {
        _db = db;
        _storage = storage;
        _extractor = extractor;
        _companies = companies;
        _audit = audit;
        _trainingWriter = trainingWriter;
        _user = user;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IEnumerable<DeliveryNoteListItemDto>> List(
        [FromQuery] DeliveryNoteStatus? status,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        var query = _db.DeliveryNotes
            .Include(n => n.AssigneeCompany)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(n => n.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            query = query.Where(n =>
                (n.DeliveryNoteNo != null && n.DeliveryNoteNo.Contains(needle)) ||
                (n.ProjectNumber != null && n.ProjectNumber.Contains(needle)) ||
                (n.OriginalFileName.Contains(needle)));
        }

        var notes = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        return notes.Select(DeliveryNoteMapper.ToListItem);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeliveryNoteDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var note = await _db.DeliveryNotes
            .Include(n => n.AssigneeCompany)
            .FirstOrDefaultAsync(n => n.Id == id, ct);
        if (note is null) return NotFound();
        return DeliveryNoteMapper.ToDetail(note);
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        var note = await _db.DeliveryNotes.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (note is null) return NotFound();
        if (!_storage.Exists(note.BlobPath)) return NotFound("Blob missing");

        var stream = await _storage.OpenAsync(note.BlobPath, ct);
        return File(stream, "application/pdf", note.OriginalFileName);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<DeliveryNoteDetailDto>> Upload(
        IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file provided.");

        string hash;
        await using (var s = file.OpenReadStream())
            hash = Convert.ToHexString(await SHA256.HashDataAsync(s, ct));

        var note = new DeliveryNote
        {
            OriginalFileName = file.FileName,
            FileSizeBytes = file.Length,
            ContentHash = hash,
            Status = DeliveryNoteStatus.Extracting,
            CreatedBy = _user.UserId,
            UpdatedBy = _user.UserId
        };

        await using (var upload = file.OpenReadStream())
            note.BlobPath = await _storage.SaveAsync(note.Id, file.FileName, upload, ct);

        _db.DeliveryNotes.Add(note);
        _db.AuditEvents.Add(_audit.BuildSimple(
            nameof(DeliveryNote), note.Id, AuditAction.Created,
            _user.UserId, AuditSource.Manual,
            payload: new { note.OriginalFileName, note.FileSizeBytes }));
        await _db.SaveChangesAsync(ct);

        await RunExtractionAsync(note, ct);

        var reloaded = await _db.DeliveryNotes
            .Include(n => n.AssigneeCompany)
            .FirstAsync(n => n.Id == note.Id, ct);
        return DeliveryNoteMapper.ToDetail(reloaded);
    }

    [HttpPost("{id:guid}/retry-extraction")]
    public async Task<ActionResult<DeliveryNoteDetailDto>> RetryExtraction(Guid id, CancellationToken ct)
    {
        var note = await _db.DeliveryNotes.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (note is null) return NotFound();
        note.Status = DeliveryNoteStatus.Extracting;
        note.ExtractionError = null;
        await _db.SaveChangesAsync(ct);

        await RunExtractionAsync(note, ct);

        var reloaded = await _db.DeliveryNotes
            .Include(n => n.AssigneeCompany)
            .FirstAsync(n => n.Id == id, ct);
        return DeliveryNoteMapper.ToDetail(reloaded);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeliveryNoteDetailDto>> Update(
        Guid id, [FromBody] UpdateDeliveryNoteDto dto, CancellationToken ct)
    {
        var note = await _db.DeliveryNotes
            .Include(n => n.AssigneeCompany)
            .FirstOrDefaultAsync(n => n.Id == id, ct);
        if (note is null) return NotFound();

        var before = CloneForAudit(note);

        note.DeliveryNoteNo = dto.DeliveryNoteNo;
        note.ProjectNumber = dto.ProjectNumber;
        note.DeliveryDate = dto.DeliveryDate;
        note.AssigneeCompanyId = dto.AssigneeCompanyId;
        note.AssigneeRawText = dto.AssigneeRawText;
        note.SupplierName = dto.SupplierName;
        note.Site = dto.Site;
        note.CostCentre = dto.CostCentre;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        note.UpdatedBy = _user.UserId;

        _db.AuditEvents.Add(_audit.Build(
            note.Id, AuditAction.Updated, before, CloneForAudit(note), _user.UserId));

        await _db.SaveChangesAsync(ct);

        var reloaded = await _db.DeliveryNotes
            .Include(n => n.AssigneeCompany)
            .FirstAsync(n => n.Id == id, ct);
        return DeliveryNoteMapper.ToDetail(reloaded);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<DeliveryNoteDetailDto>> Confirm(
        Guid id, [FromBody] ConfirmDeliveryNoteDto dto, CancellationToken ct)
    {
        var note = await _db.DeliveryNotes
            .Include(n => n.AssigneeCompany)
            .FirstOrDefaultAsync(n => n.Id == id, ct);
        if (note is null) return NotFound();

        if (string.IsNullOrWhiteSpace(note.DeliveryNoteNo) ||
            string.IsNullOrWhiteSpace(note.ProjectNumber) ||
            note.DeliveryDate is null ||
            (note.AssigneeCompanyId is null && string.IsNullOrWhiteSpace(note.AssigneeRawText)))
        {
            return BadRequest(new { message =
                "Required fields missing: deliveryNoteNo, projectNumber, deliveryDate, assignee." });
        }

        if (note.AssigneeCompanyId is null && !string.IsNullOrWhiteSpace(note.AssigneeRawText))
        {
            var normalized = note.AssigneeRawText.Trim();
            var company = await _db.Companies
                .FirstOrDefaultAsync(c => EF.Functions.Collate(c.Name, "NOCASE") == normalized, ct);
            if (company is null)
            {
                company = new Company { Name = normalized, IsActive = true };
                _db.Companies.Add(company);
                _db.AuditEvents.Add(_audit.BuildSimple(
                    nameof(Company), company.Id, AuditAction.Created,
                    _user.UserId, AuditSource.System,
                    payload: new { reason = "auto-created on delivery-note confirm", noteId = note.Id }));
            }
            note.AssigneeCompanyId = company.Id;
            note.AssigneeCompany = company;
        }

        note.Status = DeliveryNoteStatus.Confirmed;
        note.ConfirmedAt = DateTimeOffset.UtcNow;
        note.ConfirmedBy = _user.UserId;
        note.UpdatedAt = note.ConfirmedAt.Value;
        note.UpdatedBy = _user.UserId;

        _db.AuditEvents.Add(_audit.BuildSimple(
            nameof(DeliveryNote), note.Id, AuditAction.Confirmed,
            _user.UserId, AuditSource.Manual, dto.ReasonCode,
            payload: new
            {
                note.DeliveryNoteNo, note.ProjectNumber,
                deliveryDate = note.DeliveryDate?.ToString("yyyy-MM-dd"),
                assignee = note.AssigneeCompany?.Name ?? note.AssigneeRawText
            }));

        await _trainingWriter.WriteAsync(note, ct);
        await UpsertCorrectionCacheAsync(note, ct);

        await _db.SaveChangesAsync(ct);

        var reloaded = await _db.DeliveryNotes
            .Include(n => n.AssigneeCompany)
            .FirstAsync(n => n.Id == id, ct);
        return DeliveryNoteMapper.ToDetail(reloaded);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<DeliveryNoteDetailDto>> Reject(
        Guid id, [FromBody] ConfirmDeliveryNoteDto dto, CancellationToken ct)
    {
        var note = await _db.DeliveryNotes.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (note is null) return NotFound();

        note.Status = DeliveryNoteStatus.Rejected;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        note.UpdatedBy = _user.UserId;

        _db.AuditEvents.Add(_audit.BuildSimple(
            nameof(DeliveryNote), note.Id, AuditAction.Rejected,
            _user.UserId, AuditSource.Manual, dto.ReasonCode));

        await _db.SaveChangesAsync(ct);
        return DeliveryNoteMapper.ToDetail(note);
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<IEnumerable<AuditEvent>> GetAudit(Guid id, CancellationToken ct)
    {
        return await _db.AuditEvents
            .Where(a => a.EntityType == nameof(DeliveryNote) && a.EntityId == id)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(ct);
    }

    private async Task RunExtractionAsync(DeliveryNote note, CancellationToken ct)
    {
        try
        {
            await using var stream = await _storage.OpenAsync(note.BlobPath, ct);
            var extraction = await _extractor.ExtractAsync(stream, note.OriginalFileName, ct);

            note.RawExtractedJson = extraction.RawResponseJson;
            note.ModelIdUsed = extraction.ModelIdUsed;
            note.FieldConfidencesJson = JsonSerializer.Serialize(extraction.AsConfidenceMap());

            note.DeliveryNoteNo ??= extraction.DeliveryNoteNo.Value;
            note.ProjectNumber ??= extraction.ProjectNumber.Value;
            note.SupplierName ??= extraction.SupplierName.Value;
            note.Site ??= extraction.Site.Value;
            note.CostCentre ??= extraction.CostCentre.Value;

            if (note.DeliveryDate is null && !string.IsNullOrWhiteSpace(extraction.DeliveryDate.Value)
                && DateOnly.TryParse(extraction.DeliveryDate.Value, out var d))
                note.DeliveryDate = d;

            if (!string.IsNullOrWhiteSpace(extraction.Assignee.Value))
            {
                note.AssigneeRawText = extraction.Assignee.Value;
                var matched = await _companies.ResolveAsync(extraction.Assignee.Value, ct);
                if (matched is not null) note.AssigneeCompanyId = matched.Id;
            }

            var cacheApplied = await ApplyCorrectionCacheAsync(note, ct);

            note.Status = DeliveryNoteStatus.ReadyForReview;
            note.UpdatedAt = DateTimeOffset.UtcNow;

            _db.AuditEvents.Add(_audit.BuildSimple(
                nameof(DeliveryNote), note.Id, AuditAction.ExtractionCompleted,
                "system", AuditSource.Ocr,
                payload: new
                {
                    modelId = extraction.ModelIdUsed,
                    confidences = extraction.AsConfidenceMap(),
                    cacheApplied
                }));

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction failed for {Id}", note.Id);
            note.Status = DeliveryNoteStatus.ExtractionFailed;
            note.ExtractionError = ex.Message;
            note.UpdatedAt = DateTimeOffset.UtcNow;

            _db.AuditEvents.Add(_audit.BuildSimple(
                nameof(DeliveryNote), note.Id, AuditAction.ExtractionFailed,
                "system", AuditSource.Ocr,
                payload: new { error = ex.Message }));

            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<bool> ApplyCorrectionCacheAsync(DeliveryNote note, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(note.ContentHash)) return false;

        var cache = await _db.CorrectionCaches
            .FirstOrDefaultAsync(c => c.ContentHash == note.ContentHash, ct);
        if (cache is null) return false;

        note.DeliveryNoteNo = cache.DeliveryNoteNo ?? note.DeliveryNoteNo;
        note.ProjectNumber = cache.ProjectNumber ?? note.ProjectNumber;
        note.DeliveryDate = cache.DeliveryDate ?? note.DeliveryDate;
        note.AssigneeCompanyId = cache.AssigneeCompanyId ?? note.AssigneeCompanyId;
        note.AssigneeRawText = cache.AssigneeRawText ?? note.AssigneeRawText;
        note.SupplierName = cache.SupplierName ?? note.SupplierName;
        note.Site = cache.Site ?? note.Site;
        note.CostCentre = cache.CostCentre ?? note.CostCentre;

        note.ModelIdUsed = $"{note.ModelIdUsed} + correction-cache";
        cache.TimesApplied++;

        _logger.LogInformation(
            "Applied correction cache {CacheId} (source note {SourceId}) to {NoteId}",
            cache.Id, cache.SourceNoteId, note.Id);
        return true;
    }

    private async Task UpsertCorrectionCacheAsync(DeliveryNote note, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(note.ContentHash)) return;

        var existing = await _db.CorrectionCaches
            .FirstOrDefaultAsync(c => c.ContentHash == note.ContentHash, ct);

        if (existing is null)
        {
            _db.CorrectionCaches.Add(new CorrectionCache
            {
                ContentHash = note.ContentHash,
                DeliveryNoteNo = note.DeliveryNoteNo,
                ProjectNumber = note.ProjectNumber,
                DeliveryDate = note.DeliveryDate,
                AssigneeCompanyId = note.AssigneeCompanyId,
                AssigneeRawText = note.AssigneeRawText,
                SupplierName = note.SupplierName,
                Site = note.Site,
                CostCentre = note.CostCentre,
                SourceNoteId = note.Id,
                UpdatedBy = _user.UserId
            });
        }
        else
        {
            existing.DeliveryNoteNo = note.DeliveryNoteNo;
            existing.ProjectNumber = note.ProjectNumber;
            existing.DeliveryDate = note.DeliveryDate;
            existing.AssigneeCompanyId = note.AssigneeCompanyId;
            existing.AssigneeRawText = note.AssigneeRawText;
            existing.SupplierName = note.SupplierName;
            existing.Site = note.Site;
            existing.CostCentre = note.CostCentre;
            existing.SourceNoteId = note.Id;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedBy = _user.UserId;
        }
    }

    private static DeliveryNoteSnapshot CloneForAudit(DeliveryNote n) => new()
    {
        DeliveryNoteNo = n.DeliveryNoteNo,
        ProjectNumber = n.ProjectNumber,
        DeliveryDate = n.DeliveryDate?.ToString("yyyy-MM-dd"),
        AssigneeCompanyId = n.AssigneeCompanyId?.ToString(),
        AssigneeRawText = n.AssigneeRawText,
        SupplierName = n.SupplierName,
        Site = n.Site,
        CostCentre = n.CostCentre,
        Status = n.Status.ToString()
    };

    private class DeliveryNoteSnapshot
    {
        public string? DeliveryNoteNo { get; set; }
        public string? ProjectNumber { get; set; }
        public string? DeliveryDate { get; set; }
        public string? AssigneeCompanyId { get; set; }
        public string? AssigneeRawText { get; set; }
        public string? SupplierName { get; set; }
        public string? Site { get; set; }
        public string? CostCentre { get; set; }
        public string? Status { get; set; }
    }
}
