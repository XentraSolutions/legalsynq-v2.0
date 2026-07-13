using Xenia.Infrastructure.Assistant;
using Xunit;

namespace Xenia.Tests.Assistant;

public sealed class XeniaAssistantOptionsTests
{
    [Fact]
    public void OpenAiOptions_DefaultsToBlankAppSettingsApiKey()
    {
        var options = new XeniaAssistantOptions();

        Assert.Null(options.OpenAI.ApiKey);
    }
}
