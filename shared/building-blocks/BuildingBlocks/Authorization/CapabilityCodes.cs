namespace BuildingBlocks.Authorization;

public static class CapabilityCodes
{
    // ── CareConnect ───────────────────────────────────────────────────────────
    public const string ReferralCreate         = "referral:create";
    public const string ReferralReadOwn        = "referral:read:own";
    public const string ReferralCancel         = "referral:cancel";
    public const string ReferralReadAddressed  = "referral:read:addressed";
    public const string ReferralAccept         = "referral:accept";
    public const string ReferralDecline        = "referral:decline";
    public const string ReferralUpdateStatus   = "referral:update_status";
    public const string ProviderSearch         = "provider:search";
    public const string ProviderMap            = "provider:map";
    public const string ProviderManage         = "provider:manage";
    public const string AppointmentCreate      = "appointment:create";
    public const string AppointmentUpdate      = "appointment:update";
    public const string AppointmentManage      = "appointment:manage";
    public const string AppointmentReadOwn     = "appointment:read:own";
    public const string ScheduleManage         = "schedule:manage";
    public const string DashboardRead          = "dashboard:read";

    // ── SynqLien ─────────────────────────────────────────────────────────────
    public const string LienCreate    = "lien:create";
    public const string LienOffer     = "lien:offer";
    public const string LienReadOwn   = "lien:read:own";
    public const string LienBrowse    = "lien:browse";
    public const string LienPurchase  = "lien:purchase";
    public const string LienReadHeld  = "lien:read:held";
    public const string LienService   = "lien:service";
    public const string LienSettle    = "lien:settle";

    // ── SynqFund ─────────────────────────────────────────────────────────────
    public const string ApplicationCreate          = "application:create";
    public const string ApplicationReadOwn         = "application:read:own";
    public const string ApplicationCancel          = "application:cancel";
    public const string ApplicationReadAddressed   = "application:read:addressed";
    public const string ApplicationEvaluate        = "application:evaluate";
    public const string ApplicationApprove         = "application:approve";
    public const string ApplicationDecline         = "application:decline";
    public const string ApplicationStatusView      = "application:status:view";
    public const string PartyCreate                = "party:create";
    public const string PartyReadOwn               = "party:read:own";
}
