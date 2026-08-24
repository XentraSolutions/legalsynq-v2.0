namespace Identity.Application.Exceptions;

public sealed class SynqLienPortalRoleRestrictedException : Exception
{
    public SynqLienPortalRoleRestrictedException(string message) : base(message)
    {
    }
}
