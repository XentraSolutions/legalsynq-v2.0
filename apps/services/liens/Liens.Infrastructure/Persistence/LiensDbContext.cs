using BuildingBlocks.Domain;
using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Persistence;

public class LiensDbContext : DbContext
{
    public LiensDbContext(DbContextOptions<LiensDbContext> options) : base(options) { }

    public DbSet<Case> Cases => Set<Case>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<FacilityContactPerson>   FacilityContactPersons   => Set<FacilityContactPerson>();
    public DbSet<LienReduction>           LienReductions           => Set<LienReduction>();
    public DbSet<LienSettlement>          LienSettlements          => Set<LienSettlement>();
    public DbSet<SettlementPaymentDetail> SettlementPaymentDetails => Set<SettlementPaymentDetail>();
    public DbSet<DIYReportConfig>         DIYReportConfigs         => Set<DIYReportConfig>();
    public DbSet<BatchTemplate> BatchTemplates => Set<BatchTemplate>();
    public DbSet<BatchUpload> BatchUploads => Set<BatchUpload>();
    public DbSet<BatchUploadDetail> BatchUploadDetails => Set<BatchUploadDetail>();
    public DbSet<ManualMedicalCode>       ManualMedicalCodes       => Set<ManualMedicalCode>();
    public DbSet<LookupValue> LookupValues => Set<LookupValue>();
    public DbSet<Lien> Liens => Set<Lien>();
    public DbSet<LienStatusHistory> LienStatusHistories => Set<LienStatusHistory>();
    public DbSet<LienOffer> LienOffers => Set<LienOffer>();
    public DbSet<SellingPortfolio> SellingPortfolios => Set<SellingPortfolio>();
    public DbSet<SellingPortfolioLien> SellingPortfolioLiens => Set<SellingPortfolioLien>();
    public DbSet<SellingPortfolioBuyer> SellingPortfolioBuyers => Set<SellingPortfolioBuyer>();
    public DbSet<SellingPortfolioStatusHistory> SellingPortfolioStatusHistory => Set<SellingPortfolioStatusHistory>();
    public DbSet<SellingPortfolioActivity> SellingPortfolioActivities => Set<SellingPortfolioActivity>();
    public DbSet<SellingBuyerAccessLink> SellingBuyerAccessLinks => Set<SellingBuyerAccessLink>();
    public DbSet<BillOfSale> BillsOfSale => Set<BillOfSale>();
    public DbSet<ServicingItem> ServicingItems => Set<ServicingItem>();
    public DbSet<LienTask> LienTasks => Set<LienTask>();
    public DbSet<LienTaskLienLink> LienTaskLienLinks => Set<LienTaskLienLink>();
    public DbSet<LienWorkflowConfig>      LienWorkflowConfigs     => Set<LienWorkflowConfig>();
    public DbSet<LienWorkflowStage>       LienWorkflowStages      => Set<LienWorkflowStage>();
    public DbSet<LienWorkflowTransition>  LienWorkflowTransitions => Set<LienWorkflowTransition>();
    // TASK-MIG-09: LienTaskTemplates DbSet removed — liens_TaskTemplates dropped (MIG-09 migration)
    public DbSet<LienTaskGenerationRule> LienTaskGenerationRules => Set<LienTaskGenerationRule>();
    public DbSet<LienGeneratedTaskMetadata> LienGeneratedTaskMetadatas => Set<LienGeneratedTaskMetadata>();
    public DbSet<LienTaskNote> LienTaskNotes => Set<LienTaskNote>();
    public DbSet<LienCaseNote> LienCaseNotes => Set<LienCaseNote>();
    public DbSet<LegacyImportApproval> LegacyImportApprovals => Set<LegacyImportApproval>();
    public DbSet<LegacyImportRun> LegacyImportRuns => Set<LegacyImportRun>();
    public DbSet<LegacyIdCrosswalk> LegacyIdCrosswalks => Set<LegacyIdCrosswalk>();
    public DbSet<LegacyImportException> LegacyImportExceptions => Set<LegacyImportException>();
    // TASK-MIG-09: LienTaskGovernanceSettings DbSet removed — liens_TaskGovernanceSettings dropped (MIG-09 migration)

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LiensDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAtUtc == default)
                    entry.Property(nameof(AuditableEntity.CreatedAtUtc)).CurrentValue = now;

                entry.Property(nameof(AuditableEntity.UpdatedAtUtc)).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(AuditableEntity.UpdatedAtUtc)).CurrentValue = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
