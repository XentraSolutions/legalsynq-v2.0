using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Domain;

public class ReferralStatusActionsTests
{
    private static Referral MakeReferral() =>
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
            requestedService:        "PT",
            urgency:                 "Normal",
            notes:                   "General notes",
            createdByUserId:         null);

    [Fact]
    public void Decline_SetsStatusAndDeclineNotes()
    {
        var r = MakeReferral();
        r.Decline(updatedByUserId: Guid.NewGuid(), declineNotes: "Not in my specialty");

        Assert.Equal(Referral.ValidStatuses.Declined, r.Status);
        Assert.Equal("Not in my specialty", r.DeclineNotes);
    }

    [Fact]
    public void Decline_WithoutNotes_LeavesDeclineNotesNull()
    {
        var r = MakeReferral();
        r.Decline(updatedByUserId: null);

        Assert.Equal(Referral.ValidStatuses.Declined, r.Status);
        Assert.Null(r.DeclineNotes);
    }

    [Fact]
    public void Decline_TrimsDeclineNotes()
    {
        var r = MakeReferral();
        r.Decline(updatedByUserId: null, declineNotes: "  Too far away  ");

        Assert.Equal("Too far away", r.DeclineNotes);
    }

    [Fact]
    public void Decline_DoesNotOverwriteNotes()
    {
        var r = MakeReferral();
        r.Decline(updatedByUserId: null, declineNotes: "Decline reason");

        Assert.Equal("General notes", r.Notes);
        Assert.Equal("Decline reason", r.DeclineNotes);
    }

    [Fact]
    public void Complete_SetsStatusToCompleted()
    {
        var r = MakeReferral();
        var userId = Guid.NewGuid();
        r.Accept(userId);
        r.Complete(userId);

        Assert.Equal(Referral.ValidStatuses.Completed, r.Status);
    }

    [Fact]
    public void Cancel_SetsStatusToCancelled()
    {
        var r = MakeReferral();
        var userId = Guid.NewGuid();
        r.Accept(userId);
        r.Cancel(userId);

        Assert.Equal(Referral.ValidStatuses.Cancelled, r.Status);
    }

    [Fact]
    public void Create_DeclineNotesStartsNull()
    {
        var r = MakeReferral();
        Assert.Null(r.DeclineNotes);
    }
}
