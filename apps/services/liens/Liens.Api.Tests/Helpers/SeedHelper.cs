using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Helpers;

/// <summary>
/// Seeds a deterministic set of reference and test data into the InMemory DB.
/// Call once per test class after creating the service scope.
/// All IDs are stable so tests can reference them by value.
/// </summary>
public static class SeedHelper
{
    // Stable IDs used across all tests.
    public static readonly Guid TenantId  = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid UserId    = new("20000000-0000-0000-0000-000000000002");
    public static readonly Guid OrgId     = new("30000000-0000-0000-0000-000000000003");

    public static readonly Guid LawFirmId          = new("40000000-0000-0000-0000-000000000010");
    public static readonly Guid MedicalProviderId  = new("40000000-0000-0000-0000-000000000011");
    public static readonly Guid FundingCompanyId   = new("40000000-0000-0000-0000-000000000012");
    public static readonly Guid LeadContactId      = new("40000000-0000-0000-0000-000000000013");
    public static readonly Guid MedicalFacilityContactId = new("40000000-0000-0000-0000-000000000014");
    public static readonly Guid FacilityId         = new("50000000-0000-0000-0000-000000000001");
    public static readonly Guid FacilityContactId  = new("50000000-0000-0000-0000-000000000002");
    public static readonly Guid CaseId             = new("60000000-0000-0000-0000-000000000001");
    public static readonly Guid LienId             = new("70000000-0000-0000-0000-000000000001");
    public static readonly Guid ReductionId        = new("80000000-0000-0000-0000-000000000001");
    public static readonly Guid SettlementId       = new("80000000-0000-0000-0000-000000000002");
    public static readonly Guid PaymentId          = new("80000000-0000-0000-0000-000000000003");
    public static readonly Guid ReportConfigId     = new("90000000-0000-0000-0000-000000000001");
    public static readonly Guid PayoffStatementDocumentTypeId = new("90000000-0000-0000-0000-000000000002");

    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<LiensDbContext>();

        if (db.Cases.Any()) return; // already seeded

        // ── Lookup values ────────────────────────────────────────────────────
        var lookupSeed = new (string Category, string Code, string Name)[]
        {
            (LookupCategory.State,             "CA",          "California"),
            (LookupCategory.State,             "TX",          "Texas"),
            (LookupCategory.AccidentType,      "MVA",         "Motor Vehicle Accident"),
            (LookupCategory.LienStatus,        "Draft",       "Draft"),
            (LookupCategory.LienStatus,        "Active",      "Active"),
            (LookupCategory.CaseStatus,        "Open",        "Open"),
            (LookupCategory.CaseStatus,        "Closed",      "Closed"),
            (LookupCategory.MedicalStatus,     "Treating",    "Treating"),
            (LookupCategory.SettlementStatus,  "Pending",     "Pending"),
            (LookupCategory.SettlementType,    "Full",        "Full Settlement"),
            (LookupCategory.CurrentAttributes, "Active",      "Active"),
            (LookupCategory.ProcedureCode,     "99213",       "Office Visit"),
            (LookupCategory.DocumentCategory,  "Medical",     "Medical Records"),
            (LookupCategory.ServicingStatus,   "Open",        "Open"),
            (LookupCategory.ServicingPriority, "Normal",      "Normal"),
            (LookupCategory.ContactType,       ContactType.LawFirm,    "Law Firm"),
            (LookupCategory.ContactType,       ContactType.Provider,   "Medical Provider"),
            (LookupCategory.ContactType,       ContactType.MedicalFacility, "Medical Facility"),
            (LookupCategory.ContactType,       ContactType.LienHolder, "Funding Company"),
            (LookupCategory.ContactType,       ContactType.FundingCompany, "Funding Company"),
            (LookupCategory.ContactType,       ContactType.Lead,       "Lead"),
        };

        foreach (var (cat, code, name) in lookupSeed)
        {
            db.LookupValues.Add(LookupValue.Create(
                category: cat, code: code, name: name,
                createdByUserId: UserId, tenantId: TenantId, isSystem: true));
        }

        var payoffStatementDocumentType = LookupValue.Create(
            LookupCategory.DocumentCategory,
            "PayoffStatement",
            "Payoff Statement",
            UserId,
            tenantId: TenantId,
            isSystem: true);
        SetId(payoffStatementDocumentType, PayoffStatementDocumentTypeId);
        db.LookupValues.Add(payoffStatementDocumentType);

        db.ManualMedicalCodes.Add(ManualMedicalCode.Create(
            TenantId,
            "MANUAL-001",
            "Manual Procedure",
            "ASC",
            100m,
            10m,
            70m,
            30m,
            110m,
            UserId));

        // ── Contacts ─────────────────────────────────────────────────────────
        var lawFirm = Contact.Create(TenantId, OrgId, ContactType.LawFirm,
            "Smith", "Associates", UserId, organization: "Smith & Associates LLP");
        SetId(lawFirm, LawFirmId);
        db.Contacts.Add(lawFirm);

        var provider = Contact.Create(TenantId, OrgId, ContactType.Provider,
            "City", "Medical", UserId, organization: "City Medical Center");
        SetId(provider, MedicalProviderId);
        db.Contacts.Add(provider);

        var medicalFacilityContact = Contact.Create(TenantId, OrgId, ContactType.MedicalFacility,
            "Sunrise", "Clinic", UserId, organization: "Sunrise Clinic");
        SetId(medicalFacilityContact, MedicalFacilityContactId);
        db.Contacts.Add(medicalFacilityContact);

        var funder = Contact.Create(TenantId, OrgId, ContactType.LienHolder,
            "Capital", "Fund", UserId, organization: "Capital Fund LLC");
        SetId(funder, FundingCompanyId);
        db.Contacts.Add(funder);

        var lead = Contact.Create(TenantId, OrgId, ContactType.Lead,
            "Jane", "Doe", UserId, email: "jane@example.com");
        SetId(lead, LeadContactId);
        db.Contacts.Add(lead);

        // ── Facility + contact person ─────────────────────────────────────────
        var facility = Facility.Create(TenantId, OrgId, "Sunrise Clinic", UserId,
            code: "FAC001", city: "Los Angeles", state: "CA");
        SetId(facility, FacilityId);
        db.Facilities.Add(facility);

        var cp = FacilityContactPerson.Create(TenantId, FacilityId,
            "Alice", "Nurse", UserId, position: "Head Nurse", email: "alice@sunrise.com");
        SetId(cp, FacilityContactId);
        db.FacilityContactPersons.Add(cp);

        // ── Case + Lien ───────────────────────────────────────────────────────
        var caseEntity = Case.Create(TenantId, OrgId, "CASE-TEST-001",
            "John", "Plaintiff", UserId,
            title: "Test v. Defendant",
            dateOfIncident: new DateOnly(2024, 6, 15));
        SetId(caseEntity, CaseId);
        db.Cases.Add(caseEntity);

        var lien = Lien.Create(TenantId, OrgId, "LIEN-TEST-001",
            LienType.MedicalLien, 5000m, UserId, caseId: CaseId);
        SetId(lien, LienId);
        db.Liens.Add(lien);

        // ── Settlement data ───────────────────────────────────────────────────
        var reduction = LienReduction.Create(TenantId, CaseId, LienId,
            new DateOnly(2025, 1, 10), 500m, UserId, "Contractual adjustment");
        SetId(reduction, ReductionId);
        db.LienReductions.Add(reduction);

        var settlement = LienSettlement.Create(TenantId, CaseId, LienId,
            1, 4500m, UserId, "Pending", "First payment");
        SetId(settlement, SettlementId);
        db.LienSettlements.Add(settlement);

        var payment = SettlementPaymentDetail.Create(TenantId, CaseId, LienId,
            1, 4500m, UserId, new DateOnly(2025, 2, 1), "Smith Law", "CHK-1001");
        SetId(payment, PaymentId);
        db.SettlementPaymentDetails.Add(payment);

        // ── DIY report config ─────────────────────────────────────────────────
        var report = DIYReportConfig.Create(TenantId, UserId, "Open Cases",
            """{"status":"Open"}""", UserId);
        SetId(report, ReportConfigId);
        db.DIYReportConfigs.Add(report);

        await db.SaveChangesAsync();
    }

    /// <summary>Uses reflection to inject a stable ID so tests can reference it deterministically.</summary>
    private static void SetId<T>(T entity, Guid id) where T : class
    {
        var prop = typeof(T).GetProperty("Id",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, id);
    }
}
