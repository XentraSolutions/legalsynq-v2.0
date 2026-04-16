using BuildingBlocks.Domain;
using SynqComm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SynqComm.Infrastructure.Persistence;

public class SynqCommDbContext : DbContext
{
    public SynqCommDbContext(DbContextOptions<SynqCommDbContext> options) : base(options) { }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<ConversationReadState> ConversationReadStates => Set<ConversationReadState>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<EmailMessageReference> EmailMessageReferences => Set<EmailMessageReference>();
    public DbSet<ExternalParticipantIdentity> ExternalParticipantIdentities => Set<ExternalParticipantIdentity>();
    public DbSet<EmailDeliveryState> EmailDeliveryStates => Set<EmailDeliveryState>();
    public DbSet<EmailRecipientRecord> EmailRecipientRecords => Set<EmailRecipientRecord>();
    public DbSet<TenantEmailSenderConfig> TenantEmailSenderConfigs => Set<TenantEmailSenderConfig>();
    public DbSet<EmailTemplateConfig> EmailTemplateConfigs => Set<EmailTemplateConfig>();
    public DbSet<ConversationQueue> ConversationQueues => Set<ConversationQueue>();
    public DbSet<ConversationAssignment> ConversationAssignments => Set<ConversationAssignment>();
    public DbSet<ConversationSlaState> ConversationSlaStates => Set<ConversationSlaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SynqCommDbContext).Assembly);
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
