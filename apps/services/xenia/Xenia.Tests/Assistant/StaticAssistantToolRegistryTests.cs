using Xenia.Application.Assistant;
using Xenia.Infrastructure.Assistant;
using Xunit;

namespace Xenia.Tests.Assistant;

public sealed class StaticAssistantToolRegistryTests
{
    [Fact]
    public void GenericAgent_IncludesSynqLienTools()
    {
        var registry = new StaticAssistantToolRegistry();

        var tools = registry.ListToolsForAgent(AssistantModuleKeys.GenericAgentKey);

        Assert.Contains(tools, tool => tool.ToolKey == "synqlien.lien.lookup");
        Assert.Contains(tools, tool => tool.ToolKey == "synqlien.lien.search");
        Assert.Contains(tools, tool => tool.ToolKey == "synqlien.lien.queue.summary");
        Assert.Contains(tools, tool => tool.ToolKey == "synqlien.case.lookup");
        Assert.Contains(tools, tool => tool.ToolKey == "synqlien.case.search");
    }

    [Fact]
    public void SynqLienLienTools_AllowScopedReadPermissions()
    {
        var registry = new StaticAssistantToolRegistry();

        var lookup = registry
            .ListToolsForAgent(AssistantModuleKeys.LiensAgentKey)
            .Single(tool => tool.ToolKey == "synqlien.lien.lookup");

        Assert.Contains("SYNQ_LIENS.lien:read", lookup.RequiredPermissions);
        Assert.Contains("SYNQ_LIENS.lien:read:own", lookup.RequiredPermissions);
        Assert.Contains("SYNQ_LIENS.lien:browse", lookup.RequiredPermissions);
        Assert.Contains("SYNQ_LIENS.lien:read:held", lookup.RequiredPermissions);
    }
}
