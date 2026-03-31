namespace CareConnect.Application.DTOs;

public class GetReferralsQuery
{
    public string?   Status          { get; set; }
    public Guid?     ProviderId      { get; set; }
    public string?   ClientName      { get; set; }
    public string?   CaseNumber      { get; set; }
    public string?   Urgency         { get; set; }
    public DateTime? CreatedFrom     { get; set; }
    public DateTime? CreatedTo       { get; set; }
    public int       Page            { get; set; } = 1;
    public int       PageSize        { get; set; } = 20;

    // Org-participant scoping: when set, only referrals involving the specified org are returned.
    public Guid? ReferringOrgId { get; set; }
    public Guid? ReceivingOrgId { get; set; }
}
