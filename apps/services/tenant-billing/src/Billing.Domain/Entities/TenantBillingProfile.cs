namespace Billing.Domain.Entities;

/// <summary>
/// TB-DATA-01 — first-class mapping from a Tenant Billing tenant
/// (<see cref="TenantId"/>) to a Commerce-side billing account
/// (<see cref="BillingAccountId"/>). The Commerce id is stored as an opaque
/// <see cref="Guid"/> primitive: Billing keeps no project reference to
/// Commerce and never round-trips to it during resolution. The mirror fields
/// <see cref="HostPlatformKey"/> + <see cref="ExternalTenantId"/> let an
/// operator record the host-side identifiers used to provision the Commerce
/// account, useful for audits but not load-bearing for the resolver.
///
/// <para>
/// Lifecycle and uniqueness invariants are documented on
/// <see cref="TenantBillingProfileStatus"/> and enforced by both the
/// service layer and (on relational providers) a stored computed column +
/// unique index — see <c>BillingDbContext.OnModelCreating</c>.
/// </para>
/// </summary>
public sealed class TenantBillingProfile
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BillingAccountId { get; private set; }

    /// <summary>
    /// Optional host-platform identifier (e.g. "monk", "tenant-portal") that
    /// provisioned the Commerce <see cref="BillingAccountId"/>. Mirrors
    /// <c>BillingAccountExternalRef.HostPlatformKey</c> on the Commerce
    /// side. Free-form short string; not interpreted by Billing.
    /// </summary>
    public string? HostPlatformKey { get; private set; }

    /// <summary>
    /// Optional external tenant identifier the host platform used when
    /// creating the Commerce billing account (e.g. the tenant slug). Mirrors
    /// <c>BillingAccountExternalRef.ExternalTenantId</c> on the Commerce
    /// side. Useful when the Tenant Billing <see cref="TenantId"/> GUID and
    /// the host's tenant identifier differ.
    /// </summary>
    public string? ExternalTenantId { get; private set; }

    public string Status { get; private set; } = TenantBillingProfileStatus.Draft;
    public string Mode   { get; private set; } = TenantBillingMode.InternalOnly;

    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// First time the profile transitioned into <see cref="TenantBillingProfileStatus.Active"/>.
    /// Null until the first Activate call.
    /// </summary>
    public DateTime? ActivatedAtUtc { get; private set; }

    /// <summary>
    /// Set when the profile was Closed. Closed is terminal so this is a
    /// definitive timestamp.
    /// </summary>
    public DateTime? ClosedAtUtc { get; private set; }

    // EF needs a parameterless constructor.
    private TenantBillingProfile() { }

    /// <summary>
    /// Factory: create a brand-new Draft profile. The service layer is
    /// responsible for verifying that no other non-Closed profile already
    /// exists for the tenant or the billing account before calling this.
    /// </summary>
    public static TenantBillingProfile CreateDraft(
        Guid tenantId,
        Guid billingAccountId,
        string? hostPlatformKey,
        string? externalTenantId,
        string mode,
        string? notes,
        DateTime nowUtc)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must be non-empty.", nameof(tenantId));
        if (billingAccountId == Guid.Empty)
            throw new ArgumentException("BillingAccountId must be non-empty.", nameof(billingAccountId));
        if (!TenantBillingMode.IsValid(mode))
            throw new ArgumentException($"Unknown billing mode '{mode}'.", nameof(mode));

        return new TenantBillingProfile
        {
            Id               = Guid.CreateVersion7(),
            TenantId         = tenantId,
            BillingAccountId = billingAccountId,
            HostPlatformKey  = NormalizeOptional(hostPlatformKey, 100),
            ExternalTenantId = NormalizeOptional(externalTenantId, 200),
            Status           = TenantBillingProfileStatus.Draft,
            Mode             = mode,
            Notes            = NormalizeOptional(notes, 2000),
            CreatedAtUtc     = nowUtc,
            UpdatedAtUtc     = nowUtc,
        };
    }

    public void Activate(DateTime nowUtc)
    {
        if (Status == TenantBillingProfileStatus.Active) return;
        if (Status != TenantBillingProfileStatus.Draft
            && Status != TenantBillingProfileStatus.Suspended)
        {
            throw new InvalidOperationException(
                $"Cannot Activate a profile in status '{Status}'. " +
                $"Only Draft or Suspended profiles may be activated.");
        }
        Status = TenantBillingProfileStatus.Active;
        UpdatedAtUtc = nowUtc;
        ActivatedAtUtc ??= nowUtc;
    }

    public void Suspend(DateTime nowUtc)
    {
        if (Status == TenantBillingProfileStatus.Suspended) return;
        if (Status != TenantBillingProfileStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot Suspend a profile in status '{Status}'. " +
                $"Only Active profiles may be suspended.");
        }
        Status = TenantBillingProfileStatus.Suspended;
        UpdatedAtUtc = nowUtc;
    }

    public void Close(DateTime nowUtc)
    {
        if (Status == TenantBillingProfileStatus.Closed) return;
        Status = TenantBillingProfileStatus.Closed;
        UpdatedAtUtc = nowUtc;
        ClosedAtUtc = nowUtc;
    }

    public void UpdateNotes(string? notes, DateTime nowUtc)
    {
        if (Status == TenantBillingProfileStatus.Closed)
            throw new InvalidOperationException("Closed profiles are immutable.");
        Notes = NormalizeOptional(notes, 2000);
        UpdatedAtUtc = nowUtc;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"Value exceeds max length {maxLength}.");
        return trimmed;
    }
}
