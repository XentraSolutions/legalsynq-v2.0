namespace Intake.Contracts.Emails;

public static class InboundEmailRecipientTypes
{
    public const string To = "TO";
    public const string Cc = "CC";
    public const string Bcc = "BCC";
}

public static class InboundEmailCaptureStatuses
{
    public const string Captured = "CAPTURED";
}

public static class InboundEmailProcessingStatuses
{
    public const string NotStarted = "NOT_STARTED";
}