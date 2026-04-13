namespace BuildingBlocks.Authorization;

public static class PermissionCodes
{
    // ── CareConnect ───────────────────────────────────────────────────────────
    public const string ReferralCreate         = "SYNQ_CARECONNECT.referral:create";
    public const string ReferralReadOwn        = "SYNQ_CARECONNECT.referral:read:own";
    public const string ReferralCancel         = "SYNQ_CARECONNECT.referral:cancel";
    public const string ReferralReadAddressed  = "SYNQ_CARECONNECT.referral:read:addressed";
    public const string ReferralAccept         = "SYNQ_CARECONNECT.referral:accept";
    public const string ReferralDecline        = "SYNQ_CARECONNECT.referral:decline";
    public const string ReferralUpdateStatus   = "SYNQ_CARECONNECT.referral:update_status";
    public const string ProviderSearch         = "SYNQ_CARECONNECT.provider:search";
    public const string ProviderMap            = "SYNQ_CARECONNECT.provider:map";
    public const string ProviderManage         = "SYNQ_CARECONNECT.provider:manage";
    public const string AppointmentCreate      = "SYNQ_CARECONNECT.appointment:create";
    public const string AppointmentUpdate      = "SYNQ_CARECONNECT.appointment:update";
    public const string AppointmentManage      = "SYNQ_CARECONNECT.appointment:manage";
    public const string AppointmentReadOwn     = "SYNQ_CARECONNECT.appointment:read:own";
    public const string ScheduleManage         = "SYNQ_CARECONNECT.schedule:manage";
    public const string DashboardRead          = "SYNQ_CARECONNECT.dashboard:read";

    // ── SynqLien ─────────────────────────────────────────────────────────────
    public const string LienCreate    = "SYNQ_LIENS.lien:create";
    public const string LienOffer     = "SYNQ_LIENS.lien:offer";
    public const string LienReadOwn   = "SYNQ_LIENS.lien:read:own";
    public const string LienBrowse    = "SYNQ_LIENS.lien:browse";
    public const string LienPurchase  = "SYNQ_LIENS.lien:purchase";
    public const string LienReadHeld  = "SYNQ_LIENS.lien:read:held";
    public const string LienService   = "SYNQ_LIENS.lien:service";
    public const string LienSettle    = "SYNQ_LIENS.lien:settle";

    // ── SynqFund ─────────────────────────────────────────────────────────────
    public const string ApplicationCreate          = "SYNQ_FUND.application:create";
    public const string ApplicationReadOwn         = "SYNQ_FUND.application:read:own";
    public const string ApplicationCancel          = "SYNQ_FUND.application:cancel";
    public const string ApplicationReadAddressed   = "SYNQ_FUND.application:read:addressed";
    public const string ApplicationEvaluate        = "SYNQ_FUND.application:evaluate";
    public const string ApplicationApprove         = "SYNQ_FUND.application:approve";
    public const string ApplicationDecline         = "SYNQ_FUND.application:decline";
    public const string ApplicationStatusView      = "SYNQ_FUND.application:status:view";
    public const string PartyCreate                = "SYNQ_FUND.party:create";
    public const string PartyReadOwn               = "SYNQ_FUND.party:read:own";
}
