using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Contracts.DeepLinks;

/// <summary>
/// Read-only .NET consumer for the authoritative language-neutral deep-link registry.
/// This class performs contract validation only; it does not execute or navigate routes.
/// </summary>
public static class DeepLinkRouteRegistry
{
    private const string ResourceName = "Contracts.DeepLinks.routes.json";
    private static readonly Regex ParameterPattern = new(
        @":([A-Za-z][A-Za-z0-9]*)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex KeyPattern = new(
        @"^[a-z][A-Za-z0-9]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ParameterNamePattern = new(
        @"^[A-Za-z][A-Za-z0-9]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Lazy<DeepLinkRouteRegistryDocument> DocumentValue = new(Load);

    public static int Version => DocumentValue.Value.Version;

    public static IReadOnlyList<DeepLinkRouteDefinition> All => DocumentValue.Value.Routes;

    public static DeepLinkRouteDefinition? Get(string key) =>
        All.FirstOrDefault(route => string.Equals(route.Key, key, StringComparison.Ordinal));

    public static bool TryGet(string key, out DeepLinkRouteDefinition route)
    {
        var found = Get(key);
        if (found is not null)
        {
            route = found;
            return true;
        }

        route = null!;
        return false;
    }

    private static DeepLinkRouteRegistryDocument Load()
    {
        var assembly = typeof(DeepLinkRouteRegistry).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded deep-link registry '{ResourceName}' was not found in {assembly.GetName().Name}.");

        var source = JsonSerializer.Deserialize<RegistrySource>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("The embedded deep-link registry is empty.");

        if (source.Version != 1 || source.Routes is null || source.Routes.Count == 0)
            throw new InvalidOperationException("The deep-link registry must use version 1 and contain routes.");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var routes = source.Routes.Select(route => MapAndValidate(route, keys)).ToList().AsReadOnly();
        return new DeepLinkRouteRegistryDocument(source.Version, routes);
    }

    private static DeepLinkRouteDefinition MapAndValidate(
        RouteSource route,
        ISet<string> registeredKeys)
    {
        if (!KeyPattern.IsMatch(route.Key) || !registeredKeys.Add(route.Key))
            throw new InvalidOperationException(
                $"Deep-link route key '{route.Key}' is blank or duplicated.");
        if (string.IsNullOrWhiteSpace(route.PathTemplate) || !route.PathTemplate.StartsWith('/'))
            throw new InvalidOperationException(
                $"Deep-link route '{route.Key}' must use an absolute path template.");

        var requiredParameters = route.RequiredPathParameters ?? [];
        var optionalParameters = route.OptionalQueryParameters ?? [];
        var placeholders = ParameterPattern.Matches(route.PathTemplate)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToArray();

        if (placeholders.Distinct(StringComparer.Ordinal).Count() != placeholders.Length ||
            requiredParameters.Distinct(StringComparer.Ordinal).Count() != requiredParameters.Count ||
            requiredParameters.Any(name => !ParameterNamePattern.IsMatch(name)) ||
            !placeholders.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    requiredParameters.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Deep-link route '{route.Key}' placeholders must match requiredPathParameters.");
        }

        if (optionalParameters.Distinct(StringComparer.Ordinal).Count() != optionalParameters.Count ||
            optionalParameters.Any(name => !ParameterNamePattern.IsMatch(name)))
            throw new InvalidOperationException(
                $"Deep-link route '{route.Key}' has duplicate optional query parameters.");

        if (string.IsNullOrWhiteSpace(route.MobileDestination) ||
            string.IsNullOrWhiteSpace(route.FallbackDestination) ||
            string.IsNullOrWhiteSpace(route.AnalyticsEvent))
        {
            throw new InvalidOperationException(
                $"Deep-link route '{route.Key}' has incomplete metadata.");
        }

        return new DeepLinkRouteDefinition(
            route.Key,
            route.PathTemplate,
            route.MobileDestination,
            route.RequiresAuthentication,
            route.RequiresAuthorization,
            new ReadOnlyCollection<string>(requiredParameters.ToArray()),
            new ReadOnlyCollection<string>(optionalParameters.ToArray()),
            route.FallbackDestination,
            route.AnalyticsEvent,
            route.Enabled);
    }

    private sealed class RegistrySource
    {
        public int Version { get; init; }
        public List<RouteSource>? Routes { get; init; }
    }

    private sealed class RouteSource
    {
        public string Key { get; init; } = string.Empty;
        public string PathTemplate { get; init; } = string.Empty;
        public string MobileDestination { get; init; } = string.Empty;
        public bool RequiresAuthentication { get; init; }
        public bool RequiresAuthorization { get; init; }
        public List<string>? RequiredPathParameters { get; init; }
        public List<string>? OptionalQueryParameters { get; init; }
        public string FallbackDestination { get; init; } = string.Empty;
        public string AnalyticsEvent { get; init; } = string.Empty;
        public bool Enabled { get; init; }
    }
}
