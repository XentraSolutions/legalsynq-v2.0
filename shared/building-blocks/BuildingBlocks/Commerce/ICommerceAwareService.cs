namespace BuildingBlocks.Commerce;

/// <summary>
/// Optional marker interface for services that want to self-describe their
/// position in the Commerce ecosystem.
///
/// <para>
/// Implementing this interface allows the DI container, monitoring, and
/// diagnostic tooling to discover a service's Commerce metadata without
/// reflection-scanning.  The interface is additive — services that do not
/// implement it are simply absent from the Commerce ecosystem registry.
/// </para>
///
/// <para>
/// Pattern:
/// <code>
/// public sealed class MyLienService : ICommerceAwareService
/// {
///     public static readonly CommerceServiceMetadata _commerceMeta =
///         new("Synq Liens", "SYNQLIEN", null,
///             SubscriptionRequired: true,
///             MonetizationEnabled: false,
///             CommerceIntegrationActive: false);
///
///     public CommerceServiceMetadata CommerceMetadata => _commerceMeta;
/// }
/// </code>
/// </para>
/// </summary>
public interface ICommerceAwareService
{
    /// <summary>
    /// Commerce monetization metadata for this service.
    /// Should be a static or singleton value — not computed per-call.
    /// </summary>
    CommerceServiceMetadata CommerceMetadata { get; }
}
