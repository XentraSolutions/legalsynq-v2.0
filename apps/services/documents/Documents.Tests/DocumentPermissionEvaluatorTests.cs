using Documents.Application.Services;
using Documents.Domain.ValueObjects;
using Xunit;

namespace Documents.Tests;

public class DocumentPermissionEvaluatorTests
{
    [Fact]
    public void SynqLienSeller_CanReadAndUploadButCannotDelete()
    {
        var principal = new Principal
        {
            ProductRoles = ["SYNQ_LIENS:SYNQLIEN_SELLER"],
        };

        Assert.True(DocumentPermissionEvaluator.HasPermission(principal, "read"));
        Assert.True(DocumentPermissionEvaluator.HasPermission(principal, "write"));
        Assert.False(DocumentPermissionEvaluator.HasPermission(principal, "delete"));
    }

    [Theory]
    [InlineData("SYNQ_LIENS:SYNQLIEN_BUYER")]
    [InlineData("SYNQ_LIENS:SYNQLIEN_HOLDER")]
    public void OtherSynqLienRoles_DoNotGainDirectDocumentAccess(string productRole)
    {
        var principal = new Principal { ProductRoles = [productRole] };

        Assert.False(DocumentPermissionEvaluator.HasPermission(principal, "read"));
        Assert.False(DocumentPermissionEvaluator.HasPermission(principal, "write"));
    }
}
