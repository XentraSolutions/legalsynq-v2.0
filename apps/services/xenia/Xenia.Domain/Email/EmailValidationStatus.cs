namespace Xenia.Domain.Email;

/// <summary>Result of the most recent connectivity validation attempt.</summary>
public enum EmailValidationStatus
{
    /// <summary>Source has not been validated since creation or last reset.</summary>
    NotValidated,

    /// <summary>Most recent validation succeeded.</summary>
    Valid,

    /// <summary>Most recent validation failed.</summary>
    Invalid,

    /// <summary>Validation is in progress.</summary>
    Pending,
}
