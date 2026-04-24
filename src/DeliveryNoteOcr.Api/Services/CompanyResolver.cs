using DeliveryNoteOcr.Api.Data;
using DeliveryNoteOcr.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeliveryNoteOcr.Api.Services;

public interface ICompanyResolver
{
    Task<Company?> ResolveAsync(string? rawText, CancellationToken ct);
}

public class CompanyResolver : ICompanyResolver
{
    private readonly AppDbContext _db;

    public CompanyResolver(AppDbContext db) => _db = db;

    public async Task<Company?> ResolveAsync(string? rawText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        var needle = rawText.Trim().ToLowerInvariant();

        var companies = await _db.Companies
            .Include(c => c.Aliases)
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        var exact = companies.FirstOrDefault(c =>
            string.Equals(c.Name, rawText, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var aliasHit = companies.FirstOrDefault(c =>
            c.Aliases.Any(a => string.Equals(a.Alias, rawText, StringComparison.OrdinalIgnoreCase)));
        if (aliasHit is not null) return aliasHit;

        var contains = companies.FirstOrDefault(c =>
            c.Name.ToLowerInvariant().Contains(needle) || needle.Contains(c.Name.ToLowerInvariant()));
        return contains;
    }
}
