namespace BuildingBlocks.Exceptions;

public class ForbiddenException : Exception
{
    public string? CapabilityCode { get; }

    public ForbiddenException() : base("Access denied.") { }

    public ForbiddenException(string capabilityCode)
        : base($"Missing capability: {capabilityCode}")
    {
        CapabilityCode = capabilityCode;
    }
}
