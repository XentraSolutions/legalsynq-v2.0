using Microsoft.EntityFrameworkCore;
using Billing.Domain.Accounting.Erp;
using Billing.Domain.Accounting.Erp.BulkImport;
using Billing.Domain.Accounting.Erp.QuickBooks;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for the Tenant Billing service. Owns Customers, Invoices,
/// InvoiceLineItems, and Payments. Independent of any other service schema.
/// </summary>
public class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<InvoiceAdjustment> InvoiceAdjustments => Set<InvoiceAdjustment>();
    public DbSet<InvoiceTemplate> InvoiceTemplates => Set<InvoiceTemplate>();
    public DbSet<StatementTemplate> StatementTemplates => Set<StatementTemplate>();
    public DbSet<CustomerStatement> CustomerStatements => Set<CustomerStatement>();

    // MS-BILL-ERP-001 — append-safe ERP export lifecycle rows.
    public DbSet<AccountingExport> AccountingExports => Set<AccountingExport>();

    // MS-BILL-ERP-003 — operator-curated Billing↔QBO customer map.
    public DbSet<QuickBooksCustomerMapping> QuickBooksCustomerMappings
        => Set<QuickBooksCustomerMapping>();

    // MS-BILL-ERP-006 — per-import audit row written exactly once per
    // bulk-commit call. Append-only; no updates or deletes from
    // application code.
    public DbSet<BulkMappingImportHistory> BulkMappingImportHistory
        => Set<BulkMappingImportHistory>();

    // TB-DATA-01 — TenantId ↔ Commerce BillingAccountId mapping.
    public DbSet<TenantBillingProfile> TenantBillingProfiles
        => Set<TenantBillingProfile>();

    // TB-DATA-02 — local mirror of Commerce-side entitlement decision per
    // TenantBillingProfile. One current row per profile (UNIQUE on
    // TenantBillingProfileId, relational only — see OnModelCreating).
    public DbSet<TenantBillingEntitlementSnapshot> TenantBillingEntitlementSnapshots
        => Set<TenantBillingEntitlementSnapshot>();

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

            // ---- MS-BILL-WRITE-002: reversal audit fields ----
            //
            // Both nullable (legacy Recorded rows have them unset). Reason
            // bound at 1000 to mirror Refund.Reason and the service-layer
            // MaxReversalReasonLength constant; oversized values are
            // rejected with a 400 long before they reach EF.
            b.Property(x => x.ReversedAt);
            b.Property(x => x.ReversalReason).HasMaxLength(1000);

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

        // ---- InvoiceAdjustment (MS-BILL-WRITE-005) ----
        // Append-only ledger of Credit / Debit memos. The parent
        // invoice's TotalAmount, line items, and payment rows are
        // never mutated by this flow — the effective balance is
        // computed on demand from
        // (TotalAmount + sum(Debit) - sum(Credit) - sum(Payments)).
        // FK is Restrict (matches Refunds): an invoice with
        // adjustments cannot be hard-deleted without first removing
        // its adjustment ledger.
        modelBuilder.Entity<InvoiceAdjustment>(b =>
        {
            b.ToTable("invoice_adjustments");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.InvoiceId).IsRequired();
            b.Property(x => x.CustomerId).IsRequired();
            b.Property(x => x.Type).IsRequired().HasMaxLength(16);
            b.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.Reason).IsRequired().HasMaxLength(1000);
            b.Property(x => x.ReferenceNumber).HasMaxLength(64);
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.CreatedBy).HasMaxLength(200);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.InvoiceId);
            b.HasIndex(x => x.CustomerId);

            b.HasOne(x => x.Invoice)
             .WithMany(i => i.Adjustments)
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
            b.Property(x => x.BillingProfileId);

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
            b.HasIndex(x => x.BillingProfileId);
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

            // ---- MS-BILL-INT-001 — Delivery lifecycle columns -----
            // Append-only metadata; never read by render / re-derive
            // paths. Lengths mirror persistence-service trimming caps.
            b.Property(x => x.DeliveryProvider).HasMaxLength(64);
            b.Property(x => x.DeliveryStatus).HasMaxLength(32);
            b.Property(x => x.DeliveryId).HasMaxLength(200);
            b.Property(x => x.DeliveryCorrelationId).HasMaxLength(64);
            b.Property(x => x.DeliveryRecipientEmail).HasMaxLength(320);
            b.Property(x => x.DeliverySentBy).HasMaxLength(200);
            b.Property(x => x.DeliveryAttemptedAtUtc);
            b.Property(x => x.DeliveryLastSentAtUtc);
            b.Property(x => x.DeliveryFailureReason).HasMaxLength(200);
            b.Property(x => x.DeliveryRetryCount).IsRequired().HasDefaultValue(0);

            // Per-tenant uniqueness — and the only DB-level guard
            // against the MAX(seq)+1 race in the number generator.
            b.HasIndex(x => new { x.TenantId, x.StatementNumber })
                .IsUnique()
                .HasDatabaseName("UX_customer_statements_TenantId_StatementNumber");

            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.GeneratedAtUtc });
            b.HasIndex(x => new { x.TenantId, x.GeneratedAtUtc });
            b.HasIndex(x => x.TemplateId);
        });

        // ---- AccountingExport (MS-BILL-ERP-001) ----
        // Append-safe lifecycle row: INSERT in Pending state, then
        // exactly one terminal UPDATE. Duplicate prevention is
        // application-level (fingerprint match against existing
        // Exported rows) — see AccountingExportService — so this
        // mapping deliberately does NOT declare the fingerprint
        // index UNIQUE (a previous Failed row must not block a
        // re-attempt). Indexes mirror the read paths the
        // repository / projection builder expects.
        modelBuilder.Entity<AccountingExport>(b =>
        {
            b.ToTable("accounting_exports");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.Provider).IsRequired().HasMaxLength(64);
            b.Property(x => x.ExportType).IsRequired().HasMaxLength(64);
            b.Property(x => x.WindowFromUtc).IsRequired();
            b.Property(x => x.WindowToUtc).IsRequired();
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
            b.Property(x => x.ExternalReferenceId).HasMaxLength(200);
            b.Property(x => x.RequestedBy).IsRequired().HasMaxLength(200);
            b.Property(x => x.RequestedAtUtc).IsRequired();
            b.Property(x => x.CompletedAtUtc);
            b.Property(x => x.FailureReason).HasMaxLength(500);
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
            b.Property(x => x.Fingerprint).IsRequired().HasMaxLength(64);
            b.Property(x => x.InvoiceCount).IsRequired().HasDefaultValue(0);
            b.Property(x => x.PaymentCount).IsRequired().HasDefaultValue(0);
            b.Property(x => x.AdjustmentCount).IsRequired().HasDefaultValue(0);
            b.Property(x => x.JournalEntryCount).IsRequired().HasDefaultValue(0);
            b.Property(x => x.Reason).HasMaxLength(1000);

            // PayloadJson is the server-built canonical payload; can
            // be very large for big windows so it lives in LONGTEXT
            // (otherwise EF maps unbounded string to VARCHAR(255)).
            b.Property(x => x.PayloadJson).HasColumnType("LONGTEXT");

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Fingerprint });
            b.HasIndex(x => new { x.TenantId, x.RequestedAtUtc });
        });

        // ---- QuickBooksCustomerMapping (MS-BILL-ERP-003) ----
        // Operator-curated map from a Billing customer to a QBO
        // customer. Two unique indexes keep the map invertible per
        // tenant: a Billing customer cannot be double-mapped, and
        // a QBO customer cannot be linked to two distinct Billing
        // customers within the same tenant. Both indexes are named
        // so the repository can pattern-match the duplicate-key
        // exception and surface a 409 Conflict.
        modelBuilder.Entity<QuickBooksCustomerMapping>(b =>
        {
            b.ToTable("quickbooks_customer_mappings");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.BillingCustomerId).IsRequired();
            b.Property(x => x.QuickBooksCustomerId).IsRequired().HasMaxLength(100);
            b.Property(x => x.QuickBooksDisplayName).HasMaxLength(200);
            b.Property(x => x.MappingStatus).IsRequired().HasMaxLength(32);
            b.Property(x => x.ExportMode).HasMaxLength(32);
            b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(200);
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();
            b.Property(x => x.LastExportedAtUtc);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.BillingCustomerId })
                .IsUnique()
                .HasDatabaseName("UX_quickbooks_customer_mappings_TenantId_BillingCustomerId");
            b.HasIndex(x => new { x.TenantId, x.QuickBooksCustomerId })
                .IsUnique()
                .HasDatabaseName("UX_quickbooks_customer_mappings_TenantId_QuickBooksCustomerId");
        });

        // ---- BulkMappingImportHistory (MS-BILL-ERP-006) ----
        // Per-import audit row stamped exactly once per
        // BulkMappingImportService.CommitAsync call. Append-only
        // from the application layer; the SummaryJson column carries
        // the deterministic per-row outcome list (LONGTEXT to stay
        // safely above the per-row cap × per-row payload).
        modelBuilder.Entity<BulkMappingImportHistory>(b =>
        {
            b.ToTable("bulk_mapping_import_history");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.StartedAtUtc).IsRequired();
            b.Property(x => x.CompletedAtUtc).IsRequired();
            b.Property(x => x.OperatorDisplayName)
                .IsRequired()
                .HasMaxLength(200);
            b.Property(x => x.TotalRows).IsRequired();
            b.Property(x => x.AcceptedRows).IsRequired();
            b.Property(x => x.WarningRows).IsRequired();
            b.Property(x => x.RejectedRows).IsRequired();
            b.Property(x => x.SummaryJson)
                .IsRequired()
                .HasColumnType("LONGTEXT");
            b.Property(x => x.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(128);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.StartedAtUtc })
                .HasDatabaseName("IX_bulk_mapping_import_history_TenantId_StartedAtUtc");
            // Per-tenant uniqueness on the operator-supplied key so a
            // replayed commit collides at the DB level even if the
            // service-level lookup races. Closes the replay/retry
            // execution hole the ticket explicitly forbids.
            b.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("UX_bulk_mapping_import_history_TenantId_IdempotencyKey");
        });

        // ---- TenantBillingProfile (TB-DATA-01) ----
        modelBuilder.Entity<TenantBillingProfile>(b =>
        {
            b.ToTable("tenant_billing_profiles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.BillingAccountId).IsRequired();
            b.Property(x => x.HostPlatformKey).HasMaxLength(100);
            b.Property(x => x.ExternalTenantId).HasMaxLength(200);
            b.Property(x => x.Status).IsRequired().HasMaxLength(16);
            b.Property(x => x.Mode).IsRequired().HasMaxLength(32);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();
            b.Property(x => x.ActivatedAtUtc);
            b.Property(x => x.ClosedAtUtc);

            b.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_tenant_billing_profiles_TenantId");
            b.HasIndex(x => x.BillingAccountId)
                .HasDatabaseName("IX_tenant_billing_profiles_BillingAccountId");

            // Uniqueness invariants — at most one non-Closed profile per
            // tenant AND per billing account. Modeled with a stored
            // computed column that is non-null only for non-Closed rows
            // (Pomelo MySQL supports STORED generated columns), then a
            // UNIQUE index on each scope. Closed rows have NULL in the
            // scope key column and never collide, leaving an audit trail
            // of historical mappings. Same pattern used by
            // InvoiceTemplate.DefaultScopeKey.
            //
            // Skipped on the InMemory provider (used by tests); the
            // service-layer guard (HasOpenProfileForTenantAsync /
            // IsBillingAccountClaimedAsync) holds the same invariant
            // there.
            if (Database.IsRelational())
            {
                b.Property<string>("TenantOpenScopeKey")
                    .HasComputedColumnSql(
                        "(CASE WHEN `Status` <> 'Closed' THEN `TenantId` ELSE NULL END)",
                        stored: true)
                    .HasMaxLength(36);

                b.HasIndex("TenantOpenScopeKey")
                    .IsUnique()
                    .HasDatabaseName("UX_tenant_billing_profiles_TenantOpenScopeKey");

                b.Property<string>("BillingAccountOpenScopeKey")
                    .HasComputedColumnSql(
                        "(CASE WHEN `Status` <> 'Closed' THEN `BillingAccountId` ELSE NULL END)",
                        stored: true)
                    .HasMaxLength(36);

                b.HasIndex("BillingAccountOpenScopeKey")
                    .IsUnique()
                    .HasDatabaseName("UX_tenant_billing_profiles_BillingAccountOpenScopeKey");
            }
        });

        // ---- TenantBillingEntitlementSnapshot (TB-DATA-02) ----
        modelBuilder.Entity<TenantBillingEntitlementSnapshot>(b =>
        {
            b.ToTable("tenant_billing_entitlement_snapshots");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.TenantBillingProfileId).IsRequired();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.BillingAccountId).IsRequired();

            b.Property(x => x.SourceSystem).IsRequired().HasMaxLength(100);
            b.Property(x => x.SourceSnapshotId).HasMaxLength(200);
            b.Property(x => x.SourceSubscriptionId).HasMaxLength(200);
            b.Property(x => x.SourcePlanKey).HasMaxLength(100);
            b.Property(x => x.SourceProductKey).HasMaxLength(100);

            b.Property(x => x.EntitlementStatus).IsRequired().HasMaxLength(16);
            b.Property(x => x.AccessRecommendation).IsRequired().HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(1000);

            b.Property(x => x.EffectiveFromUtc);
            b.Property(x => x.EffectiveToUtc);
            b.Property(x => x.LastSyncedAtUtc).IsRequired();

            // Raw payload from the source system. Stored as LONGTEXT on
            // MySQL because it can be large; bounded only by application-
            // level validation (well-formed JSON).
            if (Database.IsRelational())
                b.Property(x => x.RawSnapshotJson).HasColumnType("LONGTEXT");

            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();

            b.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_tenant_billing_entitlement_snapshots_TenantId");
            b.HasIndex(x => x.BillingAccountId)
                .HasDatabaseName("IX_tenant_billing_entitlement_snapshots_BillingAccountId");
            b.HasIndex(x => x.EntitlementStatus)
                .HasDatabaseName("IX_tenant_billing_entitlement_snapshots_EntitlementStatus");
            b.HasIndex(x => x.AccessRecommendation)
                .HasDatabaseName("IX_tenant_billing_entitlement_snapshots_AccessRecommendation");
            b.HasIndex(x => x.LastSyncedAtUtc)
                .HasDatabaseName("IX_tenant_billing_entitlement_snapshots_LastSyncedAtUtc");

            // One current snapshot per profile. Skipped on InMemory; the
            // service-layer upsert (GetByProfileIdAsync ⇒ Add or Update)
            // holds the same invariant for tests.
            if (Database.IsRelational())
            {
                b.HasIndex(x => x.TenantBillingProfileId)
                    .IsUnique()
                    .HasDatabaseName("UX_tenant_billing_entitlement_snapshots_ProfileId");
            }
            else
            {
                b.HasIndex(x => x.TenantBillingProfileId)
                    .HasDatabaseName("IX_tenant_billing_entitlement_snapshots_ProfileId");
            }
        });
    }
}
