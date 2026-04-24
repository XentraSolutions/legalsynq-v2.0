namespace Support.Api.Notifications;

public enum NotificationDispatchMode
{
    NoOp = 0,
    Http = 1,
}

/// <summary>
/// Bound from configuration section <c>Support:Notifications</c>.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Support:Notifications";

    /// <summary>Master kill-switch; when false, all dispatch is suppressed.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Notifications Service base URL. Required when Mode = Http.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>HTTP client timeout in seconds. Defaults to 5s.</summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Dispatch transport. Defaults to NoOp for safe local/test runs.</summary>
    public NotificationDispatchMode Mode { get; set; } = NotificationDispatchMode.NoOp;
}
