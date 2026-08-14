using System.Security.Claims;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Intake.Api.Authorization;

public static class IntakeAuthorizationPolicies
{
    public const string ConfigurationRead = "IntakeConfigurationRead";
    public const string ConfigurationManage = "IntakeConfigurationManage";
    public const string SourceRead = "IntakeSourceRead";
    public const string SourceManage = "IntakeSourceManage";
    public const string EmailRead = "IntakeEmailRead";
    public const string EmailAnalytics = "IntakeEmailAnalytics";
    public const string ArtifactRead = "IntakeArtifactRead";
    public const string ArtifactManage = "IntakeArtifactManage";
    public const string ManualRead = "IntakeManualRead";
    public const string ManualManage = "IntakeManualManage";
    public const string ClassificationRead = "IntakeClassificationRead";
    public const string ClassificationManage = "IntakeClassificationManage";
    public const string ReviewRead = "IntakeReviewRead";
    public const string ReviewManage = "IntakeReviewManage";
    public const string ReviewAssign = "IntakeReviewAssign";
    public const string ReviewComplete = "IntakeReviewComplete";
    public const string SnapshotRead = "IntakeSnapshotRead";
    public const string SnapshotManage = "IntakeSnapshotManage";
    public const string AdapterExecute = "IntakeAdapterExecute";
    public const string OperationsRead = "IntakeOperationsRead";
    public const string OperationsRecover = "IntakeOperationsRecover";
    public const string OperationsAdmin = "IntakeOperationsAdmin";

    public static void AddTo(AuthorizationOptions options)
    {
        options.AddPolicy(ConfigurationRead, policy =>
            policy.RequireAuthenticatedUser());

        options.AddPolicy(ConfigurationManage, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole(Roles.PlatformAdmin) ||
                context.User.IsInRole(Roles.TenantAdmin) ||
                HasIntakeManagementClaim(context.User)));

        options.AddPolicy(SourceRead, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.sources.read") ||
                 HasPermission(context.User, "intake.sources.manage"))));

        options.AddPolicy(SourceManage, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole(Roles.PlatformAdmin) ||
                context.User.IsInRole(Roles.TenantAdmin) ||
                HasPermission(context.User, "intake.sources.manage")));

        options.AddPolicy(EmailRead, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.emails.read"))));

        options.AddPolicy(EmailAnalytics, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.emails.analytics"))));

        options.AddPolicy(ArtifactRead, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.artifacts.read") ||
                 HasPermission(context.User, "intake.artifacts.manage") ||
                 HasPermission(context.User, "intake.emails.read"))));

        options.AddPolicy(ArtifactManage, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.artifacts.manage"))));

        options.AddPolicy(ManualRead, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.manual.read") ||
                 HasPermission(context.User, "intake.manual.manage"))));

        options.AddPolicy(ManualManage, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.manual.manage"))));

        options.AddPolicy(ClassificationRead, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.classification.read") ||
                 HasPermission(context.User, "intake.classification.manage"))));

        options.AddPolicy(ClassificationManage, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.classification.manage"))));

        options.AddPolicy(ReviewRead, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.review.read") ||
                 HasPermission(context.User, "intake.review.manage"))));

        options.AddPolicy(ReviewManage, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.review.manage"))));

        options.AddPolicy(ReviewAssign, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.review.assign"))));

        options.AddPolicy(ReviewComplete, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.review.complete"))));

        options.AddPolicy(SnapshotRead, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.snapshot.read") ||
                 HasPermission(context.User, "intake.snapshot.manage"))));

        options.AddPolicy(SnapshotManage, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.snapshot.manage"))));

        options.AddPolicy(AdapterExecute, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.adapter.execute"))));

        options.AddPolicy(OperationsRead, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.operations.read") ||
                 HasPermission(context.User, "intake.operations.recover"))));

        options.AddPolicy(OperationsRecover, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 context.User.IsInRole(Roles.TenantAdmin) ||
                 HasPermission(context.User, "intake.operations.recover") ||
                 HasPermission(context.User, "intake.operations.admin"))));

        options.AddPolicy(OperationsAdmin, policy =>
            policy.RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true &&
                (context.User.IsInRole(Roles.PlatformAdmin) ||
                 HasPermission(context.User, "intake.operations.admin"))));
    }

    private static bool HasIntakeManagementClaim(ClaimsPrincipal principal) =>
        HasPermission(principal, "intake.configuration.manage");

    private static bool HasPermission(ClaimsPrincipal principal, string permission) =>
        principal.Claims.Any(claim =>
            (string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Type, "scope", StringComparison.OrdinalIgnoreCase)) &&
            claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(value => string.Equals(
                    value,
                    permission,
                    StringComparison.OrdinalIgnoreCase)));
}