using System.Collections.Concurrent;

namespace Contracts.Notifications;

/// <summary>
/// In-process, read-mostly registry of well-known notification templates
/// (E12.1). Seeded at startup with deterministic platform defaults; the
/// notifications service may add tenant overrides on top at render time
/// (handled outside this class).
///
/// <para>
/// Phrasing in the seeded defaults is intentionally generic and
/// product-neutral — per-product wording lives in tenant template
/// overrides, not in the platform registry.
/// </para>
/// </summary>
public sealed class NotificationTemplateRegistry
{
    private readonly ConcurrentDictionary<string, NotificationTemplate> _byKey =
        new(StringComparer.Ordinal);

    public NotificationTemplateRegistry(IEnumerable<NotificationTemplate>? seed = null)
    {
        foreach (var t in seed ?? PlatformDefaults())
        {
            _byKey[t.Key] = t;
        }
    }

    public NotificationTemplate? Get(string key) =>
        _byKey.TryGetValue(key, out var t) ? t : null;

    public bool TryGet(string key, out NotificationTemplate template)
    {
        if (_byKey.TryGetValue(key, out var found))
        {
            template = found;
            return true;
        }
        template = null!;
        return false;
    }

    public IReadOnlyCollection<NotificationTemplate> All() => _byKey.Values.ToList();

    /// <summary>
    /// Add or replace a template definition. Used by the notifications
    /// service when registering tenant-scoped overrides at startup.
    /// </summary>
    public void Upsert(NotificationTemplate template) => _byKey[template.Key] = template;

    // ------------------------------------------------------------------
    // Platform defaults — generic, channel-neutral phrasing.
    // ------------------------------------------------------------------

    public static IEnumerable<NotificationTemplate> PlatformDefaults()
    {
        // Workflow lifecycle
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowCompleted,
            Name            = "Workflow completed",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Workflow completed",
            BodyTemplate    = "Workflow {{workflowInstanceId}} has completed.",
            Severity        = NotificationSeverity.Info,
            Category        = NotificationCategory.Workflow,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true,
                    Description = "Identifier of the workflow instance that completed." },
                new NotificationTokenDefinition { Name = "productKey",         Required = false,
                    Description = "Product key the workflow belongs to." },
            },
        };

        // Workflow SLA transitions
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowSlaDueSoon,
            Name            = "Workflow due soon",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Workflow due soon",
            BodyTemplate    = "Workflow {{workflowInstanceId}} is due at {{dueAt}}.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Sla,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "dueAt",              Required = true,
                    Description = "ISO-8601 UTC due timestamp." },
                new NotificationTokenDefinition { Name = "productKey",         Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowSlaOverdue,
            Name            = "Workflow overdue",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Workflow overdue",
            BodyTemplate    = "Workflow {{workflowInstanceId}} is overdue (due {{dueAt}}).",
            Severity        = NotificationSeverity.Critical,
            Category        = NotificationCategory.Sla,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "dueAt",              Required = true },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowSlaEscalated,
            Name            = "Workflow escalated",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Workflow escalated",
            BodyTemplate    = "Workflow {{workflowInstanceId}} has been overdue for {{overdueMinutes}} minute(s); escalation level {{escalationLevel}}.",
            Severity        = NotificationSeverity.Critical,
            Category        = NotificationCategory.Sla,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "overdueMinutes",     Required = true },
                new NotificationTokenDefinition { Name = "escalationLevel",    Required = true },
            },
        };

        // Workflow admin actions
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowAdminRetry,
            Name            = "Workflow re-armed by admin",
            Channels        = new[] { NotificationChannels.InApp },
            SubjectTemplate = "Workflow re-armed",
            BodyTemplate    = "Workflow {{workflowInstanceId}} was re-armed by an operator: {{reason}}.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "reason",             Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowAdminForceComplete,
            Name            = "Workflow force-completed by admin",
            Channels        = new[] { NotificationChannels.InApp },
            SubjectTemplate = "Workflow force-completed",
            BodyTemplate    = "Workflow {{workflowInstanceId}} was force-completed by an operator: {{reason}}.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "reason",             Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowAdminCancel,
            Name            = "Workflow cancelled by admin",
            Channels        = new[] { NotificationChannels.InApp },
            SubjectTemplate = "Workflow cancelled",
            BodyTemplate    = "Workflow {{workflowInstanceId}} was cancelled by an operator: {{reason}}.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "reason",             Required = false },
            },
        };

        // Task lifecycle (reserved — actual emission lands in later phases).
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.TaskAssigned,
            Name            = "Task assigned",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Task assigned: {{taskTitle}}",
            BodyTemplate    = "Task '{{taskTitle}}' has been assigned to you.",
            Severity        = NotificationSeverity.Info,
            Category        = NotificationCategory.Task,
            Enabled         = false, // wiring deferred to E12.x
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "taskId",    Required = true },
                new NotificationTokenDefinition { Name = "taskTitle", Required = true },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.TaskCompleted,
            Name            = "Task completed",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Task completed: {{taskTitle}}",
            BodyTemplate    = "Task '{{taskTitle}}' has been completed.",
            Severity        = NotificationSeverity.Info,
            Category        = NotificationCategory.Task,
            Enabled         = false, // wiring deferred to E12.x
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "taskId",    Required = true },
                new NotificationTokenDefinition { Name = "taskTitle", Required = true },
            },
        };

        // ── Commerce: billing standing alerts (LS-COMMERCE-ECO-01) ──────────
        // All Commerce templates are disabled by default. Activation requires
        // Commerce integration to be wired and enabled for the tenant.
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.CommerceBillingGracePeriodStarted,
            Name            = "Billing grace period started",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Action required: payment issue on your account",
            BodyTemplate    = "Your account has entered a billing grace period due to a payment issue. " +
                              "Please update your payment method within {{graceDaysRemaining}} day(s) to avoid service interruption.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Enabled         = false, // activation deferred — requires Commerce integration wiring
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "tenantId",          Required = true },
                new NotificationTokenDefinition { Name = "graceDaysRemaining", Required = true,
                    Description = "Number of days remaining in the grace window." },
                new NotificationTokenDefinition { Name = "billingAccountId",  Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.CommerceBillingGracePeriodExpired,
            Name            = "Billing grace period expired",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Important: billing grace period has expired",
            BodyTemplate    = "Your billing grace period has expired. Access to some features may be restricted. " +
                              "Please contact your account administrator or update your payment details.",
            Severity        = NotificationSeverity.Critical,
            Category        = NotificationCategory.Admin,
            Enabled         = false,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "tenantId",         Required = true },
                new NotificationTokenDefinition { Name = "billingAccountId", Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.CommerceBillingAccountSuspended,
            Name            = "Billing account suspended",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Your account has been suspended",
            BodyTemplate    = "Your billing account has been suspended. Please contact support to resolve this issue.",
            Severity        = NotificationSeverity.Critical,
            Category        = NotificationCategory.Admin,
            Enabled         = false,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "tenantId",         Required = true },
                new NotificationTokenDefinition { Name = "billingAccountId", Required = false },
            },
        };

        // ── Commerce: subscription lifecycle ─────────────────────────────────
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.CommerceSubscriptionActivated,
            Name            = "Subscription activated",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Subscription activated: {{planName}}",
            BodyTemplate    = "Your subscription to {{planName}} has been activated. " +
                              "Your access is now fully enabled.",
            Severity        = NotificationSeverity.Info,
            Category        = NotificationCategory.Admin,
            Enabled         = false,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "tenantId",         Required = true },
                new NotificationTokenDefinition { Name = "planName",         Required = true },
                new NotificationTokenDefinition { Name = "productKey",       Required = false },
                new NotificationTokenDefinition { Name = "subscriptionId",   Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.CommerceSubscriptionRenewed,
            Name            = "Subscription renewed",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Subscription renewed: {{planName}}",
            BodyTemplate    = "Your subscription to {{planName}} has been renewed. " +
                              "Next renewal date: {{nextRenewalDate}}.",
            Severity        = NotificationSeverity.Info,
            Category        = NotificationCategory.Admin,
            Enabled         = false,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "tenantId",       Required = true },
                new NotificationTokenDefinition { Name = "planName",       Required = true },
                new NotificationTokenDefinition { Name = "nextRenewalDate", Required = false,
                    Description = "ISO-8601 UTC next renewal date." },
                new NotificationTokenDefinition { Name = "subscriptionId", Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.CommerceSubscriptionCancelled,
            Name            = "Subscription cancelled",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Subscription cancelled: {{planName}}",
            BodyTemplate    = "Your subscription to {{planName}} has been cancelled. " +
                              "Access will continue until {{accessEndsDate}}.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Enabled         = false,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "tenantId",       Required = true },
                new NotificationTokenDefinition { Name = "planName",       Required = true },
                new NotificationTokenDefinition { Name = "accessEndsDate", Required = false,
                    Description = "ISO-8601 UTC date when access expires (period end)." },
                new NotificationTokenDefinition { Name = "subscriptionId", Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.CommerceSubscriptionTrialExpiring,
            Name            = "Trial expiring soon",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Your trial expires in {{daysRemaining}} day(s)",
            BodyTemplate    = "Your trial for {{planName}} will expire in {{daysRemaining}} day(s) on {{trialEndDate}}. " +
                              "Subscribe now to maintain uninterrupted access.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Enabled         = false,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "tenantId",      Required = true },
                new NotificationTokenDefinition { Name = "planName",      Required = true },
                new NotificationTokenDefinition { Name = "daysRemaining", Required = true },
                new NotificationTokenDefinition { Name = "trialEndDate",  Required = false,
                    Description = "ISO-8601 UTC trial end date." },
            },
        };

        // ── Commerce: entitlement changes ────────────────────────────────────
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.CommerceEntitlementGranted,
            Name            = "Entitlement granted",
            Channels        = new[] { NotificationChannels.InApp },
            SubjectTemplate = "Access granted: {{productName}}",
            BodyTemplate    = "Access to {{productName}} has been granted to your account.",
            Severity        = NotificationSeverity.Info,
            Category        = NotificationCategory.Admin,
            Enabled         = false,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "tenantId",    Required = true },
                new NotificationTokenDefinition { Name = "productName", Required = true },
                new NotificationTokenDefinition { Name = "productKey",  Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.CommerceEntitlementRevoked,
            Name            = "Entitlement revoked",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Access revoked: {{productName}}",
            BodyTemplate    = "Access to {{productName}} has been revoked from your account. " +
                              "Please contact your administrator if you believe this is in error.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Enabled         = false,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "tenantId",    Required = true },
                new NotificationTokenDefinition { Name = "productName", Required = true },
                new NotificationTokenDefinition { Name = "productKey",  Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.CommerceAccessDowngraded,
            Name            = "Access level downgraded",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Your account access level has changed",
            BodyTemplate    = "Your access level has been changed to {{newAccessLevel}}. " +
                              "Some features may no longer be available. Previous level: {{previousAccessLevel}}.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Enabled         = false,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "tenantId",             Required = true },
                new NotificationTokenDefinition { Name = "newAccessLevel",       Required = true,
                    Description = "New access recommendation (Allow/ReadOnly/GraceLimited/Block)." },
                new NotificationTokenDefinition { Name = "previousAccessLevel",  Required = false },
            },
        };
    }
}
