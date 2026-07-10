namespace Xenia.Domain.Email;

/// <summary>
/// Where a secret reference points. Allows different secret backends
/// to be swapped without changing the EmailSource schema.
/// </summary>
public enum SecretReferenceType
{
    /// <summary>Reference is unresolvable in the current environment. Used for development stubs.</summary>
    Unavailable,

    /// <summary>Secret is stored in the platform's internal vault.</summary>
    PlatformVault,

    /// <summary>Secret is referenced by environment variable name.</summary>
    EnvironmentVariable,

    /// <summary>Secret is stored in an external vault (e.g. AWS Secrets Manager, HashiCorp Vault).</summary>
    ExternalVault,
}
