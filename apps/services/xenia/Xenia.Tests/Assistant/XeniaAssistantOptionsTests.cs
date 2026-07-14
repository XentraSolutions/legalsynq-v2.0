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

    [Fact]
    public void CareConnectOptions_DefaultToLocalServiceBaseUrl()
    {
        var options = new XeniaAssistantOptions();

        Assert.Equal(4, options.MaxToolIterations);
        Assert.Equal("http://127.0.0.1:5003", options.CareConnect.BaseUrl);
        Assert.Equal(20, options.CareConnect.TimeoutSeconds);
        Assert.Equal(5, options.CareConnect.MaxHistoryItems);
    }
}
