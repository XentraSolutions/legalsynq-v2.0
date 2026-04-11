namespace Identity.Infrastructure.Data;

internal static class SeedIds
{
    public static readonly DateTime SeededAt = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ── Products ──────────────────────────────────────────────────────────────
    public static readonly Guid ProductSynqFund        = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ProductSynqLiens       = new("10000000-0000-0000-0000-000000000002");
    public static readonly Guid ProductSynqCareConnect = new("10000000-0000-0000-0000-000000000003");
    public static readonly Guid ProductSynqPay         = new("10000000-0000-0000-0000-000000000004");
    public static readonly Guid ProductSynqAI          = new("10000000-0000-0000-0000-000000000005");

    // ── Tenant ────────────────────────────────────────────────────────────────
    public static readonly Guid TenantLegalSynq = new("20000000-0000-0000-0000-000000000001");

    // ── System Roles ────────────────────────────────────────────────────────
    public static readonly Guid RolePlatformAdmin = new("30000000-0000-0000-0000-000000000001");
    public static readonly Guid RoleTenantAdmin   = new("30000000-0000-0000-0000-000000000002");
    public static readonly Guid RoleStandardUser  = new("30000000-0000-0000-0000-000000000003");

    // ── Organizations ─────────────────────────────────────────────────────────
    public static readonly Guid OrgLegalSynq = new("40000000-0000-0000-0000-000000000001");

    // ── Organization Domains ──────────────────────────────────────────────────
    public static readonly Guid OrgDomainLegalSynq = new("40000000-0000-0000-0000-000000000002");

    // ── Product Roles ─────────────────────────────────────────────────────────
    public static readonly Guid PrCareConnectReferrer     = new("50000000-0000-0000-0000-000000000001");
    public static readonly Guid PrCareConnectReceiver     = new("50000000-0000-0000-0000-000000000002");
    public static readonly Guid PrSynqLienSeller          = new("50000000-0000-0000-0000-000000000003");
    public static readonly Guid PrSynqLienBuyer           = new("50000000-0000-0000-0000-000000000004");
    public static readonly Guid PrSynqLienHolder          = new("50000000-0000-0000-0000-000000000005");
    public static readonly Guid PrSynqFundReferrer        = new("50000000-0000-0000-0000-000000000006");
    public static readonly Guid PrSynqFundFunder          = new("50000000-0000-0000-0000-000000000007");
    public static readonly Guid PrSynqFundApplicantPortal = new("50000000-0000-0000-0000-000000000008");

    // ── Permissions — CareConnect ───────────────────────────────────────────
    public static readonly Guid PermReferralCreate        = new("60000000-0000-0000-0000-000000000001");
    public static readonly Guid PermReferralReadOwn       = new("60000000-0000-0000-0000-000000000002");
    public static readonly Guid PermReferralCancel        = new("60000000-0000-0000-0000-000000000003");
    public static readonly Guid PermReferralReadAddressed = new("60000000-0000-0000-0000-000000000004");
    public static readonly Guid PermReferralAccept        = new("60000000-0000-0000-0000-000000000005");
    public static readonly Guid PermReferralDecline       = new("60000000-0000-0000-0000-000000000006");
    public static readonly Guid PermProviderSearch        = new("60000000-0000-0000-0000-000000000007");
    public static readonly Guid PermProviderMap           = new("60000000-0000-0000-0000-000000000008");
    public static readonly Guid PermAppointmentCreate     = new("60000000-0000-0000-0000-000000000009");
    public static readonly Guid PermAppointmentUpdate     = new("60000000-0000-0000-0000-000000000010");
    public static readonly Guid PermAppointmentReadOwn    = new("60000000-0000-0000-0000-000000000011");

    // ── Permissions — SynqLien ──────────────────────────────────────────────
    public static readonly Guid PermLienCreate   = new("60000000-0000-0000-0000-000000000012");
    public static readonly Guid PermLienOffer    = new("60000000-0000-0000-0000-000000000013");
    public static readonly Guid PermLienReadOwn  = new("60000000-0000-0000-0000-000000000014");
    public static readonly Guid PermLienBrowse   = new("60000000-0000-0000-0000-000000000015");
    public static readonly Guid PermLienPurchase = new("60000000-0000-0000-0000-000000000016");
    public static readonly Guid PermLienReadHeld = new("60000000-0000-0000-0000-000000000017");
    public static readonly Guid PermLienService  = new("60000000-0000-0000-0000-000000000018");
    public static readonly Guid PermLienSettle   = new("60000000-0000-0000-0000-000000000019");

    // ── Organization Types ────────────────────────────────────────────────────
    public static readonly Guid OrgTypeInternal  = new("70000000-0000-0000-0000-000000000001");
    public static readonly Guid OrgTypeLawFirm   = new("70000000-0000-0000-0000-000000000002");
    public static readonly Guid OrgTypeProvider  = new("70000000-0000-0000-0000-000000000003");
    public static readonly Guid OrgTypeFunder    = new("70000000-0000-0000-0000-000000000004");
    public static readonly Guid OrgTypeLienOwner = new("70000000-0000-0000-0000-000000000005");

    // ── Relationship Types ────────────────────────────────────────────────────
    public static readonly Guid RelTypeRefersTo             = new("80000000-0000-0000-0000-000000000001");
    public static readonly Guid RelTypeAcceptsReferralsFrom = new("80000000-0000-0000-0000-000000000002");
    public static readonly Guid RelTypeFundedBy             = new("80000000-0000-0000-0000-000000000003");
    public static readonly Guid RelTypeServicesFor          = new("80000000-0000-0000-0000-000000000004");
    public static readonly Guid RelTypeAssignsLienTo        = new("80000000-0000-0000-0000-000000000005");
    public static readonly Guid RelTypeMemberOfNetwork      = new("80000000-0000-0000-0000-000000000006");

    // ── Product–RelationshipType Rules ────────────────────────────────────────
    public static readonly Guid PrRelRuleCareConnectRefersTo             = new("81000000-0000-0000-0000-000000000001");
    public static readonly Guid PrRelRuleCareConnectAcceptsReferralsFrom = new("81000000-0000-0000-0000-000000000002");
    public static readonly Guid PrRelRuleSynqFundFundedBy                = new("81000000-0000-0000-0000-000000000003");
    public static readonly Guid PrRelRuleSynqLienAssignsLienTo           = new("81000000-0000-0000-0000-000000000004");

    // ── Product–OrgType Rules ─────────────────────────────────────────────────
    public static readonly Guid PrOrgTypeRuleCareConnectReferrerLawFirm  = new("90000000-0000-0000-0000-000000000001");
    public static readonly Guid PrOrgTypeRuleCareConnectReceiverProvider = new("90000000-0000-0000-0000-000000000002");
    public static readonly Guid PrOrgTypeRuleSynqLienSellerLawFirm       = new("90000000-0000-0000-0000-000000000003");
    public static readonly Guid PrOrgTypeRuleSynqLienBuyerLienOwner      = new("90000000-0000-0000-0000-000000000004");
    public static readonly Guid PrOrgTypeRuleSynqLienHolderLienOwner     = new("90000000-0000-0000-0000-000000000005");
    public static readonly Guid PrOrgTypeRuleSynqFundReferrerLawFirm     = new("90000000-0000-0000-0000-000000000006");
    public static readonly Guid PrOrgTypeRuleSynqFundFunderFunder        = new("90000000-0000-0000-0000-000000000007");

    // ── Permissions — SynqFund ──────────────────────────────────────────────
    public static readonly Guid PermApplicationCreate        = new("60000000-0000-0000-0000-000000000020");
    public static readonly Guid PermApplicationReadOwn       = new("60000000-0000-0000-0000-000000000021");
    public static readonly Guid PermApplicationCancel        = new("60000000-0000-0000-0000-000000000022");
    public static readonly Guid PermApplicationReadAddressed = new("60000000-0000-0000-0000-000000000023");
    public static readonly Guid PermApplicationEvaluate      = new("60000000-0000-0000-0000-000000000024");
    public static readonly Guid PermApplicationApprove       = new("60000000-0000-0000-0000-000000000025");
    public static readonly Guid PermApplicationDecline       = new("60000000-0000-0000-0000-000000000026");
    public static readonly Guid PermPartyCreate              = new("60000000-0000-0000-0000-000000000027");
    public static readonly Guid PermPartyReadOwn             = new("60000000-0000-0000-0000-000000000028");
    public static readonly Guid PermApplicationStatusView    = new("60000000-0000-0000-0000-000000000029");
}
