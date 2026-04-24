using DeliveryNoteOcr.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DeliveryNoteOcr.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyAlias> CompanyAliases => Set<CompanyAlias>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<TrainingLabel> TrainingLabels => Set<TrainingLabel>();
    public DbSet<TrainingRun> TrainingRuns => Set<TrainingRun>();
    public DbSet<CorrectionCache> CorrectionCaches => Set<CorrectionCache>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var dtoConverter = new DateTimeOffsetToBinaryConverter();
        foreach (var et in b.Model.GetEntityTypes())
        {
            foreach (var p in et.GetProperties())
            {
                if (p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?))
                    p.SetValueConverter(dtoConverter);
            }
        }

        b.Entity<DeliveryNote>(e =>
        {
            e.HasIndex(x => x.ContentHash);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ProjectNumber);
            e.HasOne(x => x.AssigneeCompany).WithMany()
                .HasForeignKey(x => x.AssigneeCompanyId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Company>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.HasMany(x => x.Aliases).WithOne(x => x.Company!)
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CompanyAlias>(e =>
        {
            e.HasIndex(x => x.Alias);
        });

        b.Entity<AuditEvent>(e =>
        {
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.OccurredAt);
        });

        b.Entity<TrainingLabel>(e =>
        {
            e.HasOne(x => x.DeliveryNote).WithMany()
                .HasForeignKey(x => x.DeliveryNoteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TrainingRun).WithMany()
                .HasForeignKey(x => x.TrainingRunId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<CorrectionCache>(e =>
        {
            e.HasIndex(x => x.ContentHash).IsUnique();
        });
    }
}
