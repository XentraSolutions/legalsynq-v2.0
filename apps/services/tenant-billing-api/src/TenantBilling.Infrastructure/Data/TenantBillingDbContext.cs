using Microsoft.EntityFrameworkCore;
using TenantBilling.Domain.Entities;

namespace TenantBilling.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for the Tenant Billing service. Owns Customers, Invoices,
/// InvoiceLineItems, and Payments. Independent of any other service schema.
/// </summary>
public class TenantBillingDbContext : DbContext
{
    public TenantBillingDbContext(DbContextOptions<TenantBillingDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<InvoiceTemplate> InvoiceTemplates => Set<InvoiceTemplate>();
    public DbSet<StatementTemplate> StatementTemplates => Set<StatementTemplate>();
    public DbSet<CustomerStatement> CustomerStatements => Set<CustomerStatement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---- Customer ----
        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("customers");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Email).IsRequired().HasMaxLength(320);
            b.Property(x => x.Phone).HasMaxLength(50);
            b.Property(x => x.BillingAddress).HasMaxLength(1000);

            // INV-TPL-04: structured billing address. Bounds match
            // CustomerService normalizers; all nullable so legacy
            // rows (no structured data) remain valid.
            b.Property(x => x.BillingAddressLine1).HasMaxLength(250);
            b.Property(x => x.BillingAddressLine2).HasMaxLength(250);
            b.Property(x => x.BillingCity).HasMaxLength(100);
            b.Property(x => x.BillingStateRegion).HasMaxLength(100);
            b.Property(x => x.BillingPostalCode).HasMaxLength(100);
            b.Property(x => x.BillingCountry).HasMaxLength(100);

            b.Property(x => x.ExternalReference).HasMaxLength(200);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Email });
            b.HasIndex(x => x.IsDeleted);
        });

        // ---- Invoice ----
        modelBuilder.Entity<Invoice>(b =>
        {
            b.ToTable("invoices");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.CustomerId).IsRequired();
            b.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(64);
            b.Property(x => x.IssueDate).IsRequired();
            b.Property(x => x.DueDate).IsRequired();
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);
            b.Property(x => x.Subtotal).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.TaxAmount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2).IsRequired().HasDefaultValue(0m);
            b.Property(x => x.TotalAmount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();
            b.Property(x => x.IssuedAt);

            // ---- INV-TPL-02: template branding snapshot ----
            // Lengths/precision mirror the source columns on
            // InvoiceTemplate so the snapshot can hold whatever the
            // template held verbatim.
            b.Property(x => x.InvoiceTemplateId);
            b.Property(x => x.TemplateOwnerType).HasMaxLength(16);
            b.Property(x => x.TemplateName).HasMaxLength(200);
            b.Property(x => x.TemplateLogoUrl).HasMaxLength(1000);
            b.Property(x => x.TemplateAccentColor).HasMaxLength(7);
            b.Property(x => x.TemplateHeaderText).HasMaxLength(2000);
            b.Property(x => x.TemplateFooterText).HasMaxLength(4000);
            b.Property(x => x.TemplatePaymentInstructions).HasMaxLength(4000);
            b.Property(x => x.TemplateTermsText).HasMaxLength(8000);
            b.Property(x => x.TemplateMemoPlaceholder).HasMaxLength(2000);
            b.Property(x => x.TemplateDisplayBillingAddress)
                .IsRequired().HasDefaultValue(false);
            b.Property(x => x.TemplateDisplayPaymentInstructions)
                .IsRequired().HasDefaultValue(false);
            b.Property(x => x.TemplateDisplayTerms)
                .IsRequired().HasDefaultValue(false);
            b.Property(x => x.TemplateStampedAtUtc);

            // ---- INV-TPL-04: issuer / seller identity snapshot ----
            // Lengths mirror the source columns on InvoiceTemplate so
            // a stamp can hold whatever the template held verbatim.
            b.Property(x => x.IssuerDisplayName).HasMaxLength(200);
            b.Property(x => x.IssuerLegalName).HasMaxLength(250);
            b.Property(x => x.IssuerAddressLine1).HasMaxLength(250);
            b.Property(x => x.IssuerAddressLine2).HasMaxLength(250);
            b.Property(x => x.IssuerCity).HasMaxLength(100);
            b.Property(x => x.IssuerStateRegion).HasMaxLength(100);
            b.Property(x => x.IssuerPostalCode).HasMaxLength(100);
            b.Property(x => x.IssuerCountry).HasMaxLength(100);
            b.Property(x => x.IssuerEmail).HasMaxLength(320);
            b.Property(x => x.IssuerPhone).HasMaxLength(50);
            b.Property(x => x.IssuerTaxId).HasMaxLength(100);
            b.Property(x => x.IssuerWebsite).HasMaxLength(500);
            b.Property(x => x.IssuerStampedAtUtc);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CustomerId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.DueDate);
            b.HasIndex(x => new { x.TenantId, x.InvoiceNumber }).IsUnique();
            // Non-unique index on the snapshot reference. Powers a
            // future "list invoices stamped from template X" admin
            // query without forcing a FK (which would block template
            // deletion — see report §2.6).
            b.HasIndex(x => x.InvoiceTemplateId);

            b.HasOne(x => x.Customer)
             .WithMany(c => c.Invoices)
             .HasForeignKey(x => x.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- InvoiceLineItem ----
        modelBuilder.Entity<InvoiceLineItem>(b =>
        {
            b.ToTable("invoice_line_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.InvoiceId).IsRequired();
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
            b.Property(x => x.Quantity).IsRequired();
            b.Property(x => x.UnitPrice).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.LineTotal).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => x.InvoiceId);

            b.HasOne(x => x.Invoice)
             .WithMany(i => i.LineItems)
             .HasForeignKey(x => x.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Payment ----
        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("payments");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.InvoiceId).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.Method).IsRequired().HasMaxLength(64);
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);
            b.Property(x => x.TransactionReference).HasMaxLength(200);
            b.Property(x => x.PaidAt).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(2000);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.InvoiceId);

            // Idempotency guard: a (TenantId, TransactionReference) pair must
            // be unique so that a duplicate webhook delivery (e.g. the same
            // Stripe charge id arriving twice) cannot be recorded as two
            // separate payments and inflate an invoice's paid total. MySQL
            // treats NULLs as distinct in unique indexes, so payments without
            // a TransactionReference are not constrained by this index.
            b.HasIndex(x => new { x.TenantId, x.TransactionReference }).IsUnique();

            b.HasOne(x => x.Invoice)
             .WithMany(i => i.Payments)
             .HasForeignKey(x => x.InvoiceId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Refund ----
        modelBuilder.Entity<Refund>(b =>
        {
            b.ToTable("refunds");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.InvoiceId).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.RefundedAt).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.InvoiceId);

            b.HasOne(x => x.Invoice)
             .WithMany(i => i.Refunds)
             .HasForeignKey(x => x.InvoiceId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- InvoiceTemplate ----
        modelBuilder.Entity<InvoiceTemplate>(b =>
        {
            b.ToTable("invoice_templates");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.OwnerType).IsRequired().HasMaxLength(16);
            b.Property(x => x.BillingAccountId);
            b.Property(x => x.TenantBillingProfileId);

            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.Status).IsRequired().HasMaxLength(16);
            b.Property(x => x.IsDefault).IsRequired().HasDefaultValue(false);

            b.Property(x => x.LogoUrl).HasMaxLength(1000);
            b.Property(x => x.AccentColor).HasMaxLength(7);
            b.Property(x => x.HeaderText).HasMaxLength(2000);
            b.Property(x => x.FooterText).HasMaxLength(4000);

            b.Property(x => x.PaymentInstructions).HasMaxLength(4000);
            b.Property(x => x.TermsText).HasMaxLength(8000);
            b.Property(x => x.MemoPlaceholder).HasMaxLength(2000);

            b.Property(x => x.DefaultDueDays);
            b.Property(x => x.InvoiceNumberPrefix).HasMaxLength(20);
            b.Property(x => x.InvoiceNumberFormat).HasMaxLength(100);

            b.Property(x => x.DisplayBillingAddress).IsRequired().HasDefaultValue(true);
            b.Property(x => x.DisplayPaymentInstructions).IsRequired().HasDefaultValue(true);
            b.Property(x => x.DisplayTerms).IsRequired().HasDefaultValue(true);

            // ---- INV-TPL-04: issuer / seller identity ----
            //
            // Every column nullable. Validation lives at the service
            // layer (InvoiceTemplateValidation.NormalizeOptional* /
            // NormalizeIssuerEmail / NormalizeIssuerWebsite); the
            // EF mapping enforces only the maximum widths so a
            // direct repository write cannot exceed them.
            b.Property(x => x.IssuerDisplayName).HasMaxLength(200);
            b.Property(x => x.IssuerLegalName).HasMaxLength(250);
            b.Property(x => x.IssuerAddressLine1).HasMaxLength(250);
            b.Property(x => x.IssuerAddressLine2).HasMaxLength(250);
            b.Property(x => x.IssuerCity).HasMaxLength(100);
            b.Property(x => x.IssuerStateRegion).HasMaxLength(100);
            b.Property(x => x.IssuerPostalCode).HasMaxLength(100);
            b.Property(x => x.IssuerCountry).HasMaxLength(100);
            b.Property(x => x.IssuerEmail).HasMaxLength(320);
            b.Property(x => x.IssuerPhone).HasMaxLength(50);
            b.Property(x => x.IssuerTaxId).HasMaxLength(100);
            b.Property(x => x.IssuerWebsite).HasMaxLength(500);

            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();

            // Indexes — scope-aware lookups dominate (list by scope,
            // get default by scope), and an index on Status helps the
            // selection service skip Draft/Retired rows quickly.
            b.HasIndex(x => x.OwnerType);
            b.HasIndex(x => x.BillingAccountId);
            b.HasIndex(x => x.TenantBillingProfileId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => new { x.OwnerType, x.BillingAccountId, x.IsDefault });

            // ---- Default uniqueness guard (relational only) ----
            //
            // The service layer enforces "at most one default per scope"
            // inside an IUnitOfWork transaction, but under the default
            // MySQL isolation (REPEATABLE READ) two concurrent
            // make-default transactions could both pass their reads,
            // both unset the prior default, and both commit a new
            // default — classic write-skew. To close that gap we add
            // a stored generated column whose value is non-null only
            // for default rows, and a unique index on it. Two
            // concurrent promotions then collide at the DB level and
            // one is rejected by the duplicate-key error, which the
            // repository translates back to
            // InvoiceTemplateDefaultConflictException.
            //
            // Skipped on non-relational providers (e.g. the InMemory
            // provider used in tests) because they do not support
            // computed columns. Tests rely on the service-layer guard
            // for the same invariant.
            if (Database.IsRelational())
            {
                b.Property<string>("DefaultScopeKey")
                    .HasComputedColumnSql(
                        "(CASE WHEN `IsDefault` = 1 " +
                        "THEN CONCAT(`OwnerType`, '|', IFNULL(`BillingAccountId`, '')) " +
                        "ELSE NULL END)",
                        stored: true)
                    .HasMaxLength(64);

                b.HasIndex("DefaultScopeKey")
                    .IsUnique()
                    .HasDatabaseName("UX_invoice_templates_DefaultScopeKey");
            }
        });

        // ---- StatementTemplate (STAT-B02) ----
        modelBuilder.Entity<StatementTemplate>(b =>
        {
            b.ToTable("statement_templates");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.Status).IsRequired().HasMaxLength(16);
            b.Property(x => x.IsDefault).IsRequired().HasDefaultValue(false);

            b.Property(x => x.LogoUrl).HasMaxLength(1000);
            b.Property(x => x.AccentColor).HasMaxLength(7);
            b.Property(x => x.HeaderText).HasMaxLength(2000);
            b.Property(x => x.FooterText).HasMaxLength(4000);

            b.Property(x => x.PaymentInstructions).HasMaxLength(4000);
            b.Property(x => x.TermsText).HasMaxLength(8000);
            b.Property(x => x.MemoPlaceholder).HasMaxLength(2000);

            b.Property(x => x.DisplayOutstandingTable).IsRequired().HasDefaultValue(true);
            b.Property(x => x.DisplayPaymentInstructions).IsRequired().HasDefaultValue(true);
            b.Property(x => x.DisplayTransactionMemos).IsRequired().HasDefaultValue(true);

            b.Property(x => x.StatementNumberPrefix).HasMaxLength(20);

            b.Property(x => x.IssuerDisplayName).HasMaxLength(200);
            b.Property(x => x.IssuerLegalName).HasMaxLength(250);
            b.Property(x => x.IssuerAddressLine1).HasMaxLength(250);
            b.Property(x => x.IssuerAddressLine2).HasMaxLength(250);
            b.Property(x => x.IssuerCity).HasMaxLength(100);
            b.Property(x => x.IssuerStateRegion).HasMaxLength(100);
            b.Property(x => x.IssuerPostalCode).HasMaxLength(100);
            b.Property(x => x.IssuerCountry).HasMaxLength(100);
            b.Property(x => x.IssuerEmail).HasMaxLength(320);
            b.Property(x => x.IssuerPhone).HasMaxLength(50);
            b.Property(x => x.IssuerTaxId).HasMaxLength(100);
            b.Property(x => x.IssuerWebsite).HasMaxLength(500);

            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => new { x.TenantId, x.IsDefault });

            // Default-uniqueness guard. Tenant-only scope (no
            // platform tier), so the computed key is just the
            // TenantId itself; the unique index then ensures at
            // most one default per tenant. Skipped on non-relational
            // providers — see InvoiceTemplate for the same rationale.
            if (Database.IsRelational())
            {
                b.Property<string>("DefaultScopeKey")
                    .HasComputedColumnSql(
                        "(CASE WHEN `IsDefault` = 1 " +
                        "THEN CAST(`TenantId` AS CHAR(36)) " +
                        "ELSE NULL END)",
                        stored: true)
                    .HasMaxLength(36);

                b.HasIndex("DefaultScopeKey")
                    .IsUnique()
                    .HasDatabaseName("UX_statement_templates_DefaultScopeKey");
            }
        });

        // ---- CustomerStatement (STAT-B02) ----
        modelBuilder.Entity<CustomerStatement>(b =>
        {
            b.ToTable("customer_statements");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.CustomerId).IsRequired();
            b.Property(x => x.StatementNumber).IsRequired().HasMaxLength(32);
            b.Property(x => x.TemplateId);

            b.Property(x => x.PeriodStart).IsRequired();
            b.Property(x => x.PeriodEnd).IsRequired();
            b.Property(x => x.GeneratedAtUtc).IsRequired();
            b.Property(x => x.Status).IsRequired().HasMaxLength(16);

            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.OpeningBalance).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.ClosingBalance).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.OutstandingBalance).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.TotalInvoiced).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.TotalPaid).HasPrecision(18, 2).IsRequired();

            // Snapshots stored as JSON text. LONGTEXT on MySQL — set
            // explicitly because the default for an unbounded string
            // is VARCHAR(255).
            b.Property(x => x.StatementSnapshotJson).IsRequired().HasColumnType("LONGTEXT");
            b.Property(x => x.TemplateSnapshotJson).HasColumnType("LONGTEXT");
            b.Property(x => x.HtmlSnapshot).HasColumnType("LONGTEXT");

            b.Property(x => x.VoidedAtUtc);
            b.Property(x => x.VoidReason).HasMaxLength(1000);

            // Per-tenant uniqueness — and the only DB-level guard
            // against the MAX(seq)+1 race in the number generator.
            b.HasIndex(x => new { x.TenantId, x.StatementNumber })
                .IsUnique()
                .HasDatabaseName("UX_customer_statements_TenantId_StatementNumber");

            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.GeneratedAtUtc });
            b.HasIndex(x => new { x.TenantId, x.GeneratedAtUtc });
            b.HasIndex(x => x.TemplateId);
        });
    }
}
