namespace SynqComm.Domain.Enums;

public static class SlaTriggerType
{
    public const string FirstResponseWarning = "synqcomm_first_response_warning";
    public const string FirstResponseBreach = "synqcomm_first_response_breach";
    public const string ResolutionWarning = "synqcomm_resolution_warning";
    public const string ResolutionBreach = "synqcomm_resolution_breach";

    public static readonly IReadOnlyList<string> All = new[]
    {
        FirstResponseWarning, FirstResponseBreach,
        ResolutionWarning, ResolutionBreach
    };
}
