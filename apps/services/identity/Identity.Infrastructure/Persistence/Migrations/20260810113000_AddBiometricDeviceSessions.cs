using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260810113000_AddBiometricDeviceSessions")]
public sealed class AddBiometricDeviceSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE `idt_DeviceSessions` (
                `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
                `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
                `RefreshTokenHash` varchar(64) NOT NULL,
                `TokenFamilyId` char(36) COLLATE ascii_general_ci NOT NULL,
                `Status` varchar(20) NOT NULL,
                `CreatedAtUtc` datetime(6) NOT NULL,
                `LastUsedAtUtc` datetime(6) NOT NULL,
                `AbsoluteExpiresAtUtc` datetime(6) NOT NULL,
                `InactivityExpiresAtUtc` datetime(6) NOT NULL,
                `RevokedAtUtc` datetime(6) NULL,
                `RevokedReason` varchar(64) NULL,
                `Platform` varchar(20) NOT NULL,
                `AppVersion` varchar(32) NOT NULL,
                `OsVersion` varchar(32) NOT NULL,
                `DeviceDisplayName` varchar(128) NOT NULL,
                `BiometricEnabled` tinyint(1) NOT NULL DEFAULT FALSE,
                `LastPrimaryAuthenticationAtUtc` datetime(6) NOT NULL,
                `RiskState` varchar(20) NOT NULL DEFAULT 'Normal',
                `RowVersion` bigint NOT NULL DEFAULT 0,
                CONSTRAINT `PK_idt_DeviceSessions` PRIMARY KEY (`Id`),
                CONSTRAINT `FK_idt_DeviceSessions_idt_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `idt_Users` (`Id`) ON DELETE CASCADE
            ) CHARACTER SET utf8mb4;

            CREATE UNIQUE INDEX `IX_idt_DeviceSessions_RefreshTokenHash` ON `idt_DeviceSessions` (`RefreshTokenHash`);
            CREATE INDEX `IX_idt_DeviceSessions_UserId` ON `idt_DeviceSessions` (`UserId`);
            CREATE INDEX `IX_idt_DeviceSessions_TokenFamilyId` ON `idt_DeviceSessions` (`TokenFamilyId`);

            CREATE TABLE `idt_RefreshTokenLedgerEntries` (
                `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                `DeviceSessionId` char(36) COLLATE ascii_general_ci NOT NULL,
                `TokenFamilyId` char(36) COLLATE ascii_general_ci NOT NULL,
                `TokenHash` varchar(64) NOT NULL,
                `Status` varchar(20) NOT NULL,
                `IssuedAtUtc` datetime(6) NOT NULL,
                `RotatedAtUtc` datetime(6) NULL,
                `RotatedIntoLedgerEntryId` char(36) COLLATE ascii_general_ci NULL,
                CONSTRAINT `PK_idt_RefreshTokenLedgerEntries` PRIMARY KEY (`Id`),
                CONSTRAINT `FK_idt_RTLE_DeviceSession` FOREIGN KEY (`DeviceSessionId`) REFERENCES `idt_DeviceSessions` (`Id`) ON DELETE CASCADE
            ) CHARACTER SET utf8mb4;

            CREATE UNIQUE INDEX `IX_idt_RefreshTokenLedgerEntries_TokenHash` ON `idt_RefreshTokenLedgerEntries` (`TokenHash`);
            CREATE INDEX `IX_idt_RefreshTokenLedgerEntries_TokenFamilyId` ON `idt_RefreshTokenLedgerEntries` (`TokenFamilyId`);
            CREATE INDEX `IX_idt_RefreshTokenLedgerEntries_DeviceSessionId` ON `idt_RefreshTokenLedgerEntries` (`DeviceSessionId`);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS `idt_RefreshTokenLedgerEntries`;
            DROP TABLE IF EXISTS `idt_DeviceSessions`;
            """);
    }
}
