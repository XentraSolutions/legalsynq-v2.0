using System.Text.RegularExpressions;

namespace Identity.Domain;

public enum ProvisioningStatus
{
    Pending,
    InProgress,
    Provisioned,
    Verifying,
    Active,
    Failed
}

public enum ProvisioningFailureStage
{
    None,
    DnsProvisioning,
    DnsVerification,
    HttpVerification
}

public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public int? SessionTimeoutMinutes { get; private set; }

    public Guid? LogoDocumentId { get; private set; }
    public Guid? LogoWhiteDocumentId { get; private set; }

    public string? Subdomain { get; private set; }
    public ProvisioningStatus ProvisioningStatus { get; private set; }
    public DateTime? LastProvisioningAttemptUtc { get; private set; }
    public string? ProvisioningFailureReason { get; private set; }
    public ProvisioningFailureStage ProvisioningFailureStage { get; private set; }

    public int VerificationAttemptCount { get; private set; }
    public DateTime? LastVerificationAttemptUtc { get; private set; }
    public DateTime? NextVerificationRetryAtUtc { get; private set; }
    public bool IsVerificationRetryExhausted { get; private set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PreferredSubdomain { get; private set; }

    public ICollection<User> Users { get; private set; } = [];
    public ICollection<Role> Roles { get; private set; } = [];
    public ICollection<TenantProduct> TenantProducts { get; private set; } = [];
    public ICollection<Organization> Organizations { get; private set; } = [];
    public ICollection<TenantDomain> Domains { get; private set; } = [];

    private Tenant() { }

    public static Tenant Create(string name, string code, string? preferredSubdomain = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var slug = SlugGenerator.Normalize(code);

        var now = DateTime.UtcNow;
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = slug,
            IsActive = true,
            Subdomain = null,
            PreferredSubdomain = slug,
            ProvisioningStatus = ProvisioningStatus.Pending,
            ProvisioningFailureStage = ProvisioningFailureStage.None,
            VerificationAttemptCount = 0,
            IsVerificationRetryExhausted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void SetSessionTimeout(int? minutes)
    {
        if (minutes.HasValue && (minutes.Value < 5 || minutes.Value > 480))
            throw new ArgumentOutOfRangeException(nameof(minutes), "Session timeout must be between 5 and 480 minutes.");
        SessionTimeoutMinutes = minutes;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetLogo(Guid documentId)
    {
        LogoDocumentId = documentId;
        UpdatedAtUtc   = DateTime.UtcNow;
    }

    public void ClearLogo()
    {
        LogoDocumentId = null;
        UpdatedAtUtc   = DateTime.UtcNow;
    }

    public void SetLogoWhite(Guid documentId)
    {
        LogoWhiteDocumentId = documentId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ClearLogoWhite()
    {
        LogoWhiteDocumentId = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkProvisioningInProgress()
    {
        ProvisioningStatus = ProvisioningStatus.InProgress;
        LastProvisioningAttemptUtc = DateTime.UtcNow;
        ProvisioningFailureReason = null;
        ProvisioningFailureStage = ProvisioningFailureStage.None;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkProvisioningProvisioned()
    {
        ProvisioningStatus = ProvisioningStatus.Provisioned;
        ProvisioningFailureReason = null;
        ProvisioningFailureStage = ProvisioningFailureStage.None;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkProvisioningVerifying()
    {
        ProvisioningStatus = ProvisioningStatus.Verifying;
        ProvisioningFailureReason = null;
        ProvisioningFailureStage = ProvisioningFailureStage.None;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkProvisioningActive()
    {
        ProvisioningStatus = ProvisioningStatus.Active;
        ProvisioningFailureReason = null;
        ProvisioningFailureStage = ProvisioningFailureStage.None;
        NextVerificationRetryAtUtc = null;
        IsVerificationRetryExhausted = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkProvisioningFailed(string reason, ProvisioningFailureStage stage = ProvisioningFailureStage.None)
    {
        ProvisioningStatus = ProvisioningStatus.Failed;
        ProvisioningFailureReason = reason;
        ProvisioningFailureStage = stage;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordVerificationAttempt(string? failureReason, ProvisioningFailureStage failureStage)
    {
        VerificationAttemptCount++;
        LastVerificationAttemptUtc = DateTime.UtcNow;
        ProvisioningFailureReason = failureReason;
        ProvisioningFailureStage = failureStage;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ScheduleVerificationRetry(DateTime nextRetryUtc)
    {
        ProvisioningStatus = ProvisioningStatus.Verifying;
        NextVerificationRetryAtUtc = nextRetryUtc;
        IsVerificationRetryExhausted = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkVerificationRetryExhausted(string reason, ProvisioningFailureStage stage)
    {
        ProvisioningStatus = ProvisioningStatus.Failed;
        ProvisioningFailureReason = reason;
        ProvisioningFailureStage = stage;
        NextVerificationRetryAtUtc = null;
        IsVerificationRetryExhausted = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ResetVerificationRetryState()
    {
        VerificationAttemptCount = 0;
        LastVerificationAttemptUtc = null;
        NextVerificationRetryAtUtc = null;
        IsVerificationRetryExhausted = false;
        ProvisioningFailureReason = null;
        ProvisioningFailureStage = ProvisioningFailureStage.None;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetSubdomain(string slug)
    {
        Subdomain = slug;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public static class SlugGenerator
{
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "app", "mail", "email", "ftp", "ssh",
        "portal", "login", "auth", "dashboard", "help", "support",
        "docs", "status", "blog", "cdn", "static", "assets",
        "staging", "dev", "test", "demo", "sandbox",
        "legalsynq", "legal-synq", "platform"
    };

    private static readonly Regex ValidSlugPattern = new(
        @"^[a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?$",
        RegexOptions.Compiled);

    public static string Generate(string tenantName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);
        var slug = Normalize(tenantName);
        if (string.IsNullOrEmpty(slug))
            slug = "tenant";
        return slug;
    }

    public static string Normalize(string input)
    {
        var slug = input
            .Trim()
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');

        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = Regex.Replace(slug, @"-{2,}", "-");
        slug = slug.Trim('-');

        if (slug.Length > 63)
            slug = slug[..63].TrimEnd('-');

        return slug;
    }

    public static (bool IsValid, string? Error) Validate(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return (false, "Subdomain is required.");

        if (slug.Length < 2)
            return (false, "Subdomain must be at least 2 characters.");

        if (slug.Length > 63)
            return (false, "Subdomain must be at most 63 characters.");

        if (!ValidSlugPattern.IsMatch(slug))
            return (false, "Subdomain must contain only lowercase letters, numbers, and hyphens. Cannot start or end with a hyphen.");

        if (ReservedSlugs.Contains(slug))
            return (false, $"'{slug}' is a reserved name and cannot be used as a subdomain.");

        return (true, null);
    }

    public static string AppendSuffix(string slug, int attempt)
    {
        var suffix = $"-{attempt}";
        var maxBase = 63 - suffix.Length;
        var baseSlug = slug.Length > maxBase ? slug[..maxBase].TrimEnd('-') : slug;
        return baseSlug + suffix;
    }
}
