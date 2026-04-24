using DeliveryNoteOcr.Api.Data;
using DeliveryNoteOcr.Api.Data.Entities;
using DeliveryNoteOcr.Api.Domain;
using DeliveryNoteOcr.Api.Dtos;
using DeliveryNoteOcr.Api.Services;
using DeliveryNoteOcr.Api.Services.Audit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DeliveryNoteOcr.Api.Dtos.DeliveryNoteMapper;

namespace DeliveryNoteOcr.Api.Controllers;

[ApiController]
[Route("api/companies")]
public class CompaniesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public CompaniesController(AppDbContext db, ICurrentUser user, IAuditService audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IEnumerable<CompanyDto>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var query = _db.Companies.Include(c => c.Aliases).AsQueryable();
        if (!includeInactive) query = query.Where(c => c.IsActive);
        var items = await query.OrderBy(c => c.Name).ToListAsync(ct);
        return items.Select(Map);
    }

    [HttpPost]
    public async Task<ActionResult<CompanyDto>> Create(
        [FromBody] CreateCompanyDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name required.");

        var trimmed = dto.Name.Trim();
        if (await _db.Companies.AnyAsync(
                c => EF.Functions.Collate(c.Name, "NOCASE") == trimmed, ct))
            return Conflict(new { message = "A company with that name already exists." });

        var company = new Company
        {
            Name = dto.Name.Trim(),
            ExternalCode = dto.ExternalCode?.Trim(),
            Aliases = (dto.Aliases ?? new()).Select(a => new CompanyAlias { Alias = a.Trim() }).ToList()
        };
        _db.Companies.Add(company);
        _db.AuditEvents.Add(_audit.BuildSimple(
            nameof(Company), company.Id, AuditAction.Created, _user.UserId,
            AuditSource.Manual, payload: new { company.Name, company.ExternalCode }));

        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = company.Id }, Map(company));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> Get(Guid id, CancellationToken ct)
    {
        var c = await _db.Companies.Include(x => x.Aliases)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        return Map(c);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> Update(
        Guid id, [FromBody] UpdateCompanyDto dto, CancellationToken ct)
    {
        var c = await _db.Companies.Include(x => x.Aliases)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();

        var before = new { c.Name, c.ExternalCode, c.IsActive };

        if (!string.IsNullOrWhiteSpace(dto.Name)) c.Name = dto.Name.Trim();
        if (dto.ExternalCode is not null) c.ExternalCode = dto.ExternalCode.Trim();
        if (dto.IsActive.HasValue) c.IsActive = dto.IsActive.Value;
        c.UpdatedAt = DateTimeOffset.UtcNow;

        if (dto.Aliases is not null)
        {
            _db.CompanyAliases.RemoveRange(c.Aliases);
            c.Aliases = dto.Aliases.Select(a => new CompanyAlias { Alias = a.Trim() }).ToList();
        }

        _db.AuditEvents.Add(_audit.BuildSimple(
            nameof(Company), c.Id, AuditAction.Updated, _user.UserId,
            AuditSource.Manual,
            payload: new { before, after = new { c.Name, c.ExternalCode, c.IsActive } }));

        await _db.SaveChangesAsync(ct);
        return Map(c);
    }

    [HttpGet("{id:guid}/delivery-notes")]
    public async Task<ActionResult<IEnumerable<DeliveryNoteListItemDto>>> DeliveryNotes(
        Guid id, CancellationToken ct)
    {
        if (!await _db.Companies.AnyAsync(c => c.Id == id, ct))
            return NotFound();

        var notes = await _db.DeliveryNotes
            .Include(n => n.AssigneeCompany)
            .Where(n => n.AssigneeCompanyId == id)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        return Ok(notes.Select(ToListItem));
    }

    private static CompanyDto Map(Company c) => new(
        c.Id, c.Name, c.ExternalCode, c.IsActive,
        c.Aliases.Select(a => a.Alias).ToList());
}
