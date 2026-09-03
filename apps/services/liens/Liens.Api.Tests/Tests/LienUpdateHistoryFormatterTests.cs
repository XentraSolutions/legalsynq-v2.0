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

    [Fact]
    public void BuildSingleDescription_preserves_every_changed_field_in_one_row()
    {
        var changes = Enumerable.Range(1, 12)
            .Select(index => new LienFieldChange(
                $"Changed Field {index}",
                new string('a', 150),
                new string('b', 150)))
            .ToArray();

        var description = LienUpdateHistoryFormatter.BuildSingleDescription("Lien Update", changes);

        description.Should().StartWith("Lien Update. Changes:");
        description.Length.Should().BeGreaterThan(500);
        foreach (var change in changes)
            description.Should().Contain($"{change.Field}:");
    }
}
