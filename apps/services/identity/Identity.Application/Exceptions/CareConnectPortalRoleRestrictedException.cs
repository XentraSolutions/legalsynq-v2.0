namespace Identity.Application.Exceptions;

public sealed class CareConnectPortalRoleRestrictedException : Exception
{
    public CareConnectPortalRoleRestrictedException(string message)
        : base(message)
    {
    }
}
