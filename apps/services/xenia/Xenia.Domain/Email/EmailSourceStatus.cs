namespace Xenia.Domain.Email;

/// <summary>Operational status of an email source.</summary>
public enum EmailSourceStatus
{
    /// <summary>Source is active and available for use.</summary>
    Active,

    /// <summary>Source has been disabled by the tenant administrator.</summary>
    Disabled,

    /// <summary>Source encountered an error during validation or operation.</summary>
    Error,

    /// <summary>Source is currently undergoing validation.</summary>
    Validating,

    /// <summary>Source has been created but not yet validated.</summary>
    Pending,
}
