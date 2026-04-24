namespace Support.Api.Domain;

public enum TicketStatus
{
    Open,
    Pending,
    InProgress,
    Resolved,
    Closed,
    Cancelled
}

public enum TicketPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public enum TicketSeverity
{
    Sev4,
    Sev3,
    Sev2,
    Sev1
}

public enum TicketSource
{
    Portal,
    Email,
    Chat,
    Phone,
    Monitoring,
    External
}
