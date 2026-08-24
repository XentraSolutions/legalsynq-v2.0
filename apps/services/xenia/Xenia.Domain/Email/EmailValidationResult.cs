namespace Xenia.Domain.Email;

/// <summary>Result enumeration for a single validation attempt.</summary>
public enum EmailValidationResult
{
    /// <summary>Validation completed successfully — connection confirmed.</summary>
    Success,

    /// <summary>Configuration is invalid (missing fields, bad format).</summary>
    ConfigurationInvalid,

    /// <summary>Credentials could not be resolved or are invalid.</summary>
    CredentialInvalid,

    /// <summary>Host or port is unreachable within the allowed timeout.</summary>
    ConnectionFailed,

    /// <summary>TLS negotiation failed.</summary>
    TlsFailed,

    /// <summary>The host name or address is not allowed (SSRF policy, loopback, private range).</summary>
    HostNotAllowed,

    /// <summary>The secret reference could not be resolved.</summary>
    SecretUnavailable,

    /// <summary>The validation infrastructure is not available in this environment.</summary>
    ValidatorUnavailable,

    /// <summary>Validation timed out.</summary>
    Timeout,

    /// <summary>An unexpected error occurred. Detail is sanitized.</summary>
    InternalError,
}
