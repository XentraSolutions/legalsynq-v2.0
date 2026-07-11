namespace Xenia.Domain.Automation;

/// <summary>
/// Extensible set of capabilities an automation provider may declare.
/// Stored as comma-separated string in persistence; extensible without schema migration.
/// </summary>
[Flags]
public enum AutomationCapability : long
{
    None                      = 0,
    Triggerable               = 1L << 0,
    Schedulable               = 1L << 1,
    EventDriven               = 1L << 2,
    ManualExecution           = 1L << 3,
    BatchExecution            = 1L << 4,
    Streaming                 = 1L << 5,
    UsesDocuments             = 1L << 6,
    UsesNotifications         = 1L << 7,
    UsesWorkflow              = 1L << 8,
    UsesAI                    = 1L << 9,
    SupportsRetry             = 1L << 10,
    SupportsCancellation      = 1L << 11,
    SupportsDiagnostics       = 1L << 12,
    SupportsTenantConfiguration  = 1L << 13,
    SupportsPlatformConfiguration = 1L << 14,
}
