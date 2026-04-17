using BuildingBlocks.Context;
using Flow.Domain.Interfaces;

namespace Flow.Api.Services;

/// <summary>
/// LS-FLOW-MERGE-P3 — adapts the platform <see cref="ICurrentRequestContext"/>
/// into the Flow.Domain-defined <see cref="IFlowUserContext"/>.
/// </summary>
public sealed class FlowUserContext : IFlowUserContext
{
    private readonly ICurrentRequestContext _ctx;

    public FlowUserContext(ICurrentRequestContext ctx)
    {
        _ctx = ctx;
    }

    public string? TenantId => _ctx.TenantId?.ToString("D").ToLowerInvariant();
    public string? UserId => _ctx.UserId?.ToString("D");
}
