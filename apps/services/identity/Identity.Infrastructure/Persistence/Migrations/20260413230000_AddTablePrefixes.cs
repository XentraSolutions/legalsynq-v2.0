using Microsoft.EntityFrameworkCore.Migrations;

  #nullable disable

  namespace Identity.Infrastructure.Persistence;

  public partial class AddTablePrefixes : Migration
  {
      protected override void Up(MigrationBuilder migrationBuilder)
      {
              migrationBuilder.RenameTable(name: "AccessGroups", newName: "idt_AccessGroups");
            migrationBuilder.RenameTable(name: "AccessGroupMemberships", newName: "idt_AccessGroupMemberships");
            migrationBuilder.RenameTable(name: "AuditLogs", newName: "idt_AuditLogs");
            migrationBuilder.RenameTable(name: "Capabilities", newName: "idt_Capabilities");
            migrationBuilder.RenameTable(name: "GroupProductAccess", newName: "idt_GroupProductAccess");
            migrationBuilder.RenameTable(name: "GroupRoleAssignments", newName: "idt_GroupRoleAssignments");
            migrationBuilder.RenameTable(name: "Organizations", newName: "idt_Organizations");
            migrationBuilder.RenameTable(name: "OrganizationDomains", newName: "idt_OrganizationDomains");
            migrationBuilder.RenameTable(name: "OrganizationProducts", newName: "idt_OrganizationProducts");
            migrationBuilder.RenameTable(name: "OrganizationRelationships", newName: "idt_OrganizationRelationships");
            migrationBuilder.RenameTable(name: "OrganizationTypes", newName: "idt_OrganizationTypes");
            migrationBuilder.RenameTable(name: "PasswordResetTokens", newName: "idt_PasswordResetTokens");
            migrationBuilder.RenameTable(name: "PermissionPolicies", newName: "idt_PermissionPolicies");
            migrationBuilder.RenameTable(name: "Policies", newName: "idt_Policies");
            migrationBuilder.RenameTable(name: "PolicyRules", newName: "idt_PolicyRules");
            migrationBuilder.RenameTable(name: "Products", newName: "idt_Products");
            migrationBuilder.RenameTable(name: "ProductOrganizationTypeRules", newName: "idt_ProductOrganizationTypeRules");
            migrationBuilder.RenameTable(name: "ProductRelationshipTypeRules", newName: "idt_ProductRelationshipTypeRules");
            migrationBuilder.RenameTable(name: "ProductRoles", newName: "idt_ProductRoles");
            migrationBuilder.RenameTable(name: "RelationshipTypes", newName: "idt_RelationshipTypes");
            migrationBuilder.RenameTable(name: "Roles", newName: "idt_Roles");
            migrationBuilder.RenameTable(name: "RoleCapabilities", newName: "idt_RoleCapabilities");
            migrationBuilder.RenameTable(name: "RoleCapabilityAssignments", newName: "idt_RoleCapabilityAssignments");
            migrationBuilder.RenameTable(name: "ScopedRoleAssignments", newName: "idt_ScopedRoleAssignments");
            migrationBuilder.RenameTable(name: "Tenants", newName: "idt_Tenants");
            migrationBuilder.RenameTable(name: "TenantDomains", newName: "idt_TenantDomains");
            migrationBuilder.RenameTable(name: "TenantProducts", newName: "idt_TenantProducts");
            migrationBuilder.RenameTable(name: "TenantProductEntitlements", newName: "idt_TenantProductEntitlements");
            migrationBuilder.RenameTable(name: "Users", newName: "idt_Users");
            migrationBuilder.RenameTable(name: "UserInvitations", newName: "idt_UserInvitations");
            migrationBuilder.RenameTable(name: "UserOrganizationMemberships", newName: "idt_UserOrganizationMemberships");
            migrationBuilder.RenameTable(name: "UserProductAccess", newName: "idt_UserProductAccess");
            migrationBuilder.RenameTable(name: "UserRoleAssignments", newName: "idt_UserRoleAssignments");
      }

      protected override void Down(MigrationBuilder migrationBuilder)
      {
              migrationBuilder.RenameTable(name: "idt_AccessGroups", newName: "AccessGroups");
            migrationBuilder.RenameTable(name: "idt_AccessGroupMemberships", newName: "AccessGroupMemberships");
            migrationBuilder.RenameTable(name: "idt_AuditLogs", newName: "AuditLogs");
            migrationBuilder.RenameTable(name: "idt_Capabilities", newName: "Capabilities");
            migrationBuilder.RenameTable(name: "idt_GroupProductAccess", newName: "GroupProductAccess");
            migrationBuilder.RenameTable(name: "idt_GroupRoleAssignments", newName: "GroupRoleAssignments");
            migrationBuilder.RenameTable(name: "idt_Organizations", newName: "Organizations");
            migrationBuilder.RenameTable(name: "idt_OrganizationDomains", newName: "OrganizationDomains");
            migrationBuilder.RenameTable(name: "idt_OrganizationProducts", newName: "OrganizationProducts");
            migrationBuilder.RenameTable(name: "idt_OrganizationRelationships", newName: "OrganizationRelationships");
            migrationBuilder.RenameTable(name: "idt_OrganizationTypes", newName: "OrganizationTypes");
            migrationBuilder.RenameTable(name: "idt_PasswordResetTokens", newName: "PasswordResetTokens");
            migrationBuilder.RenameTable(name: "idt_PermissionPolicies", newName: "PermissionPolicies");
            migrationBuilder.RenameTable(name: "idt_Policies", newName: "Policies");
            migrationBuilder.RenameTable(name: "idt_PolicyRules", newName: "PolicyRules");
            migrationBuilder.RenameTable(name: "idt_Products", newName: "Products");
            migrationBuilder.RenameTable(name: "idt_ProductOrganizationTypeRules", newName: "ProductOrganizationTypeRules");
            migrationBuilder.RenameTable(name: "idt_ProductRelationshipTypeRules", newName: "ProductRelationshipTypeRules");
            migrationBuilder.RenameTable(name: "idt_ProductRoles", newName: "ProductRoles");
            migrationBuilder.RenameTable(name: "idt_RelationshipTypes", newName: "RelationshipTypes");
            migrationBuilder.RenameTable(name: "idt_Roles", newName: "Roles");
            migrationBuilder.RenameTable(name: "idt_RoleCapabilities", newName: "RoleCapabilities");
            migrationBuilder.RenameTable(name: "idt_RoleCapabilityAssignments", newName: "RoleCapabilityAssignments");
            migrationBuilder.RenameTable(name: "idt_ScopedRoleAssignments", newName: "ScopedRoleAssignments");
            migrationBuilder.RenameTable(name: "idt_Tenants", newName: "Tenants");
            migrationBuilder.RenameTable(name: "idt_TenantDomains", newName: "TenantDomains");
            migrationBuilder.RenameTable(name: "idt_TenantProducts", newName: "TenantProducts");
            migrationBuilder.RenameTable(name: "idt_TenantProductEntitlements", newName: "TenantProductEntitlements");
            migrationBuilder.RenameTable(name: "idt_Users", newName: "Users");
            migrationBuilder.RenameTable(name: "idt_UserInvitations", newName: "UserInvitations");
            migrationBuilder.RenameTable(name: "idt_UserOrganizationMemberships", newName: "UserOrganizationMemberships");
            migrationBuilder.RenameTable(name: "idt_UserProductAccess", newName: "UserProductAccess");
            migrationBuilder.RenameTable(name: "idt_UserRoleAssignments", newName: "UserRoleAssignments");
      }
  }
  