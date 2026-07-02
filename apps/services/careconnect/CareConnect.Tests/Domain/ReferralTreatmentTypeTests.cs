using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Domain;

/// <summary>
/// Tests for the Type of Treatment feature on the Referral domain entity.
///
///   Referral.Create:
///     - RequestedService is null when not provided (nullable after this feature)
///     - RequestedService is trimmed when provided
///     - TreatmentTypeId starts null on creation
///
///   Referral.Update — TreatmentTypeId:
///     - Setting a non-empty Guid stores it
///     - Calling with treatmentTypeId=null, clearTreatmentType=false leaves it unchanged
///     - Calling with clearTreatmentType=true sets it to null
///     - Setting overrides a previously-set value
///     - Clear after a previous value sets it to null
///     - Guid.Empty is NOT passed to Update directly (the service converts it to
///       clearTreatmentType=true before calling Update — not tested here)
///
///   Referral.Update — RequestedService nullable:
///     - Passing null clears RequestedService
///     - Passing whitespace-only is trimmed to empty string
/// </summary>
public class ReferralTreatmentTypeTests
{
    private static Referral MakeReferral(string? requestedService = "Initial Service") =>
        Referral.Create(
            tenantId:                Guid.NewGuid(),
            referringOrganizationId: null,
            receivingOrganizationId: null,
            providerId:              Guid.NewGuid(),
            subjectPartyId:          null,
            subjectNameSnapshot:     null,
            subjectDobSnapshot:      null,
            clientFirstName:         "Jane",
            clientLastName:          "Doe",
            clientDob:               null,
            clientPhone:             "555-0100",
            clientEmail:             "",
            caseNumber:              null,
            requestedService:        requestedService,
            urgency:                 "Normal",
            notes:                   null,
            createdByUserId:         null);

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WhenRequestedServiceIsNull_StoresNull()
    {
        var r = MakeReferral(requestedService: null);
        Assert.Null(r.RequestedService);
    }

    [Fact]
    public void Create_WhenRequestedServiceProvided_IsTrimmed()
    {
        var r = MakeReferral(requestedService: "  Physical Therapy  ");
        Assert.Equal("Physical Therapy", r.RequestedService);
    }

    [Fact]
    public void Create_TreatmentTypeId_IsNullByDefault()
    {
        var r = MakeReferral();
        Assert.Null(r.TreatmentTypeId);
    }

    // ── Update — TreatmentTypeId ──────────────────────────────────────────────

    [Fact]
    public void Update_WithTreatmentTypeId_StoresValue()
    {
        var r    = MakeReferral();
        var ttId = Guid.NewGuid();

        r.Update("Service", "Normal", "Accepted", null, null, treatmentTypeId: ttId);

        Assert.Equal(ttId, r.TreatmentTypeId);
    }

    [Fact]
    public void Update_WithNoTreatmentArgs_LeavesExistingValueUnchanged()
    {
        var r    = MakeReferral();
        var ttId = Guid.NewGuid();
        r.Update("Service", "Normal", "New", null, null, treatmentTypeId: ttId);

        // No treatment args — should not change TreatmentTypeId
        r.Update("Service", "Normal", "Accepted", null, null);

        Assert.Equal(ttId, r.TreatmentTypeId);
    }

    [Fact]
    public void Update_WithClearTreatmentType_SetsToNull()
    {
        var r    = MakeReferral();
        var ttId = Guid.NewGuid();
        r.Update("Service", "Normal", "New", null, null, treatmentTypeId: ttId);

        r.Update("Service", "Normal", "Accepted", null, null, clearTreatmentType: true);

        Assert.Null(r.TreatmentTypeId);
    }

    [Fact]
    public void Update_SetThenOverwrite_StoresNewValue()
    {
        var r    = MakeReferral();
        var id1  = Guid.NewGuid();
        var id2  = Guid.NewGuid();

        r.Update("Service", "Normal", "New", null, null, treatmentTypeId: id1);
        r.Update("Service", "Normal", "New", null, null, treatmentTypeId: id2);

        Assert.Equal(id2, r.TreatmentTypeId);
    }

    [Fact]
    public void Update_ClearWhenAlreadyNull_RemainsNull()
    {
        var r = MakeReferral();
        Assert.Null(r.TreatmentTypeId);

        r.Update("Service", "Normal", "New", null, null, clearTreatmentType: true);

        Assert.Null(r.TreatmentTypeId);
    }

    // ── Update — RequestedService nullable ────────────────────────────────────

    [Fact]
    public void Update_WithNullRequestedService_ClearsIt()
    {
        var r = MakeReferral("Initial");

        r.Update(null, "Normal", "Accepted", null, null);

        Assert.Null(r.RequestedService);
    }

    [Fact]
    public void Update_WithWhitespaceRequestedService_TrimsToEmpty()
    {
        var r = MakeReferral("Initial");

        r.Update("   ", "Normal", "Accepted", null, null);

        Assert.Equal("", r.RequestedService);
    }
}
