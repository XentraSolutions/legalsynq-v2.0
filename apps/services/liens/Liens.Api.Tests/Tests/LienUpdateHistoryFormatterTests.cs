using Liens.Application.Services;

namespace Liens.Api.Tests.Tests;

public sealed class LienUpdateHistoryFormatterTests
{
    [Fact]
    public void BuildDescriptions_preserves_every_changed_field_within_storage_limit()
    {
        var changes = Enumerable.Range(1, 12)
            .Select(index => new LienFieldChange(
                $"Changed Field {index}",
                new string('a', 150),
                new string('b', 150)))
            .ToArray();

        var descriptions = LienUpdateHistoryFormatter.BuildDescriptions("Lien updated", changes);

        descriptions.Should().HaveCountGreaterThan(1);
        descriptions.Should().OnlyContain(description => description.Length <= 500);
        var combined = string.Join(' ', descriptions);
        foreach (var change in changes)
            combined.Should().Contain($"{change.Field}:");
    }
}
