namespace Liens.Api.Tests.Infrastructure;

public sealed class LegacyReportParityImportContractTests
{
    [Fact]
    public void Complete_import_uses_case_manager_lineage_and_typed_parity_columns()
    {
        var sql = ReadImportScript("import-sl-core-complete.sql");

        sql.Should().Contain("'SL_CASE_MANAGER' AS LegacySourceTable");
        sql.Should().Contain("CASE_ATTORNEY REGEXP '^[0-9]+$'");
        sql.Should().Contain("ClientAddressLine1, ClientCity, ClientState, ClientPostalCode");
        sql.Should().Contain("ClientPhone, ClientEmail, Address");
        sql.Should().Contain("TrackingFollowUpDate, MinorComp, CaseDropped, ImportedCreatedByName");
        sql.Should().Contain("LEFT(NULLIF(TRIM(lm.LM_CREATE_BY), ''), 100) AS ImportedCreatedByName");
        sql.Should().Contain("lm.LM_NOTE, lm.LM_CREATE_BY, lm.LM_CREATED, lm.LM_UPDATED");
        sql.Should().Contain("sl-core-contact-facility-v2");
    }

    [Fact]
    public void Contact_v3_upgrade_is_manifest_bound_and_merges_only_the_approved_alias()
    {
        var sql = ReadImportScript("import-sl-core-contacts-facilities-tenant-only.sql");

        sql.Should().Contain("SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci");
        sql.Should().Contain("v_core_mapping_manifest_hash <> v_mapping_manifest_hash");
        sql.Should().Contain("BINARY LOWER(SOURCE_FINGERPRINT) = BINARY v_source_fingerprint");
        sql.Should().Contain("BINARY x.LegacyId = BINARY CAST(c.CONTACT_ID AS CHAR)");
        sql.Should().Contain("s.FirstName COLLATE utf8mb4_0900_ai_ci");
        sql.Should().Contain("CREATE TEMPORARY TABLE tmp_sl_core_contacts (");
        sql.Should().NotContain("CREATE TEMPORARY TABLE tmp_sl_core_contacts AS");
        sql.Should().Contain("INSERT INTO tmp_sl_core_contacts (");
        sql.Should().NotContain("UNION ALL\n    SELECT\n        'SL_CASE_MANAGER'");
        sql.Should().Contain("'SL_CASE_MANAGER' AS LegacySourceTable");
        sql.Should().Contain("tmp_sl_core_case_person_case_law_firms");
        sql.Should().Contain("COUNT(DISTINCT refs.LegacyLawFirmId) = 1");
        sql.Should().Contain("COALESCE(law_firm_parent.TargetContactId,");
        sql.Should().Contain("case_law_firm_parent.TargetContactId");
        sql.Should().NotContain("CAST(COALESCE(law_firm_parent.TargetContactId,");
        sql.Should().Contain("sl-core-contact-facility-v3");
        sql.Should().Contain("LegacyContactId = 602");
        sql.Should().Contain("LegacyContactId = 792");
        sql.Should().Contain("staged.IsCanonical = FALSE");
        sql.Should().Contain("MergedContactAliasRows");
        sql.Should().Contain("approved alias SL_CASE_MANAGER:792->602");
        sql.Should().Contain("CanonicalExistingTargetId <> AliasExistingTargetId");
        sql.Should().Contain("LSLTC-031 approved contact aliases map to different target contacts");
        sql.Should().Contain("LSLTC-032 approved contact aliases must belong to the same v3 import run");
        sql.Should().Contain("approved.CanonicalTargetContactId");
        sql.Should().Contain("approved.AliasLegacyContactId = staged.LegacyContactId");
        sql.Should().Contain("'Contact', TargetContactId, SourceHash");
        sql.Should().Contain("mapped_target.Id = s.ExistingTargetId");
        sql.Should().Contain("v_contact_crosswalks_to_insert <> 0");
        sql.Should().Contain("v_contact_crosswalks_to_repair <> 0");
        sql.Should().Contain("WHERE IsCanonical");
        sql.Should().Contain("AS LegacySourceHash");
        sql.Should().Contain("hash_run.MappingVersion IN ('sl-core-contact-facility-v1', 'sl-core-contact-facility-v2')");
        sql.Should().Contain("BINARY s.ExistingSourceHash = BINARY s.LegacySourceHash");
        sql.Should().Contain("LegacyLienFacilityHashMatches");
        sql.Should().Contain("BINARY s.ExistingSourceHash = BINARY s.SourceHash");
        sql.Should().Contain("LOWER(prior_run.SourceFingerprint) = v_source_fingerprint");
        sql.Should().Contain("prior_run.OrgId = v_org_id");
    }

    [Fact]
    public void Imported_creator_backfill_is_crosswalk_bound_and_conflict_guarded()
    {
        var sql = ReadImportScript("backfill-sl-core-imported-creators.sql");

        sql.Should().Contain("SL_CASE' AND x.TargetEntity = 'Case'");
        sql.Should().Contain("SL_LEINS_MEDICAL' AND x.TargetEntity = 'Lien'");
        sql.Should().Contain("INNER JOIN liens_LegacyIdCrosswalks x ON x.TenantId = v_tenant_id");
        sql.Should().NotContain("'MissingCrosswalk'");
        sql.Should().Contain("LEFT(NULLIF(TRIM(source.CASE_CREATE_BY), ''), 100)");
        sql.Should().Contain("LEFT(NULLIF(TRIM(source.LM_CREATE_BY), ''), 100)");
        sql.Should().Contain("target.ImportedCreatedByName IS NULL OR TRIM(target.ImportedCreatedByName) = ''");
        sql.Should().Contain("creator backfill has conflicts; no rows were changed");
        sql.Should().Contain("expected change count does not match dry run");
        sql.Should().Contain("apply 20260825180000_AddLienImportedCreatedByName before this backfill");
        sql.Should().Contain("HEX(x.LegacyId) = HEX(CAST(source.CASE_ID AS CHAR))");
        sql.Should().Contain("HEX(target.ImportedCreatedByName) = HEX(LEFT(NULLIF(TRIM(source.LM_CREATE_BY), ''), 100))");
    }

    [Fact]
    public void Incident_state_backfill_is_crosswalk_bound_and_conflict_guarded()
    {
        var sql = ReadImportScript("backfill-sl-core-incident-state.sql");

        sql.Should().Contain("CASE_ACCIDENT_STATE");
        sql.Should().Contain("target.IncidentState IS NULL OR TRIM(target.IncidentState) = ''");
        sql.Should().Contain("SL_CASE' AND x.TargetEntity = 'Case'");
        sql.Should().Contain("INNER JOIN liens_LegacyIdCrosswalks x ON x.TenantId = v_tenant_id");
        sql.Should().Contain("HEX(x.LegacyId) = HEX(CAST(source.CASE_ID AS CHAR))");
        sql.Should().Contain("HEX(target.IncidentState) = HEX(LEFT(NULLIF(TRIM(source.CASE_ACCIDENT_STATE), ''), 100))");
        sql.Should().Contain("incident-state backfill has conflicts; no rows were changed");
        sql.Should().Contain("expected change count does not match dry run");
        sql.Should().Contain("apply 20260825160000_AddLegacyReportParityFields before this backfill");
    }

    [Fact]
    public void Medical_code_backfill_is_tenant_bound_and_requires_a_dry_run_count()
    {
        var sql = ReadImportScript("backfill-sl-core-medical-code-amounts.sql");

        sql.Should().Contain("DATABASE() NOT IN ('LS_QA_LIENS', 'LS_LIENS')");
        sql.Should().NotContain("USE LS_LIENS;");
        sql.Should().Contain("IN p_expected_changes INT");
        sql.Should().Contain("HEX(x.LegacyId) = HEX(CAST(mc.LMC_LM_ID AS CHAR))");
        sql.Should().Contain("HEX(LOWER(SOURCE_FINGERPRINT)) = HEX(v_source_fingerprint)");
        sql.Should().Contain("expected change count does not match dry run");
        sql.Should().Contain("medical-code backfill has conflicts; no rows were changed");
        sql.Should().Contain("TaskNumber, 'LegacyMedicalCode'");
        sql.Should().Contain("COALESCE(CAST(BillingText AS DECIMAL(20,2)), 0)");
    }

    [Fact]
    public void Plaintiff_address_and_lawfirm_email_backfill_is_crosswalk_bound_and_conflict_guarded()
    {
        var sql = ReadImportScript("backfill-sl-core-plaintiff-address-and-lawfirm-email.sql");

        sql.Should().Contain("CASE_ADDRESS");
        sql.Should().Contain("CASE_CITY");
        sql.Should().Contain("CASE_STATE");
        sql.Should().Contain("CASE_ZIPCODE");
        sql.Should().Contain("CONTACT_EMAIL");
        sql.Should().Contain("SourceTable='SL_CONTACT'");
        sql.Should().Contain("law-firm contact crosswalks disagree across completed imports");
        sql.Should().Contain("SELECT DISTINCT c.Id TargetId");
        sql.Should().Contain("sl-core-contact-facility-v1','sl-core-contact-facility-v2','sl-core-contact-facility-v3");
        sql.Should().Contain("ClientPostalCode");
        sql.Should().Contain("NULLIF(CONCAT_WS(', ', NULLIF(TRIM(s.CASE_ADDRESS),'')");
        sql.Should().Contain("c.ClientAddress=COALESCE(NULLIF(TRIM(c.ClientAddress),''),s.FullAddress)");
        sql.Should().Contain("NULLIF(TRIM(c.ClientAddressLine1),'')");
        sql.Should().Contain("plaintiff-address/law-firm-email backfill has conflicts");
        sql.Should().Contain("expected change count does not match dry run");
    }

    [Fact]
    public void Case_contact_and_sex_backfill_is_crosswalk_bound_and_conflict_guarded()
    {
        var sql = ReadImportScript("backfill-sl-core-case-contact-and-sex.sql");

        sql.Should().Contain("CASE_PHONE");
        sql.Should().Contain("CASE_EMAIL");
        sql.Should().Contain("CASE_GENDER");
        sql.Should().Contain("ClientPhone=COALESCE(NULLIF(TRIM(c.ClientPhone),'')");
        sql.Should().Contain("ClientEmail=COALESCE(NULLIF(TRIM(c.ClientEmail),'')");
        sql.Should().Contain("gender=");
        sql.Should().Contain("PhoneConflict");
        sql.Should().Contain("EmailConflict");
        sql.Should().Contain("SexConflict");
        sql.Should().Contain("NoSourceContactOrSex");
        sql.Should().Contain("case contact/sex backfill has conflicts");
    }

    [Fact]
    public void Report_party_and_facility_backfill_restores_crosswalk_bound_report_details()
    {
        var sql = ReadImportScript("backfill-sl-core-report-party-and-facility-details.sql");

        sql.Should().Contain("SL_CASE_MANAGER");
        sql.Should().Contain("CM_EMAIL");
        sql.Should().Contain("SL_CONTACT");
        sql.Should().Contain("CONTACT_PHONE");
        sql.Should().Contain("SL_LEINS_MEDICAL_INFORMATION_FACILITY");
        sql.Should().Contain("SL_FACILITY");
        sql.Should().Contain("FACILITY_ADDRESS");
        sql.Should().Contain("caseManagerId=");
        sql.Should().Contain("LienFacilityConflict");
        sql.Should().Contain("report-party/facility backfill has conflicts");
    }

    [Fact]
    public void Relationship_backfill_is_conflict_guarded_and_records_hashed_parity_evidence()
    {
        var sql = ReadImportScript("backfill-sl-core-case-relationships.sql");

        sql.Should().Contain("source_manager.CM_ID = source_case.CASE_MANAGER");
        sql.Should().Contain("source_law_firm.CONTACT_ID = source_case.CASE_LAW_FIRM");
        sql.Should().Contain("law_firm_x.SourceTable = 'SL_CONTACT'");
        sql.Should().Contain("law_firm_run.MappingVersion IN ('sl-core-contact-facility-v1', 'sl-core-contact-facility-v2', 'sl-core-contact-facility-v3')");
        sql.Should().Contain("NeedsLawFirm");
        sql.Should().Contain("CONCAT('lawFirmId=', TargetLawFirmId)");
        sql.Should().Contain("target_case.ClientDob AS ExistingClientDob");
        sql.Should().Contain("target_case.ClientDob = COALESCE(target_case.ClientDob, staged.SourceClientDob)");
        sql.Should().Contain("tmp_sl_core_report_medical_codes");
        sql.Should().Contain("tmp_sl_core_report_providers");
        sql.Should().Contain("tmp_sl_core_report_activities");
        sql.Should().Contain("'SL_CASE_NOTES_LAST_ACTIVITY'");
        sql.Should().Contain("@medical_code_rows_inserted = @medical_code_insert_count");
        sql.Should().Contain("@provider_rows_inserted = @provider_insert_count");
        sql.Should().Contain("@activity_rows_inserted = @activity_insert_count");
        sql.Should().Contain("attorney_x.SourceTable = 'SL_CASE_MANAGER'");
        sql.Should().Contain("r.MappingVersion IN ('sl-core-contact-facility-v2', 'sl-core-contact-facility-v3')");
        sql.Should().Contain("facility_run.MappingVersion IN ('sl-core-contact-facility-v1', 'sl-core-contact-facility-v2', 'sl-core-contact-facility-v3')");
        sql.Should().Contain("link_run.MappingVersion IN ('sl-core-contact-facility-v1', 'sl-core-contact-facility-v2', 'sl-core-contact-facility-v3')");
        sql.Should().Contain("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE");
        sql.Should().Contain("liens_LegacyFieldMigrationStates");
        sql.Should().Contain("SHA2(CAST(JSON_ARRAY(");
        sql.Should().Contain("ParityLedgerHasIncompleteTarget");
        sql.Should().Contain("@parity_ledger_rows_inserted = @parity_field_updates");
        sql.Should().NotContain("\nCOMMIT;");
    }

    [Fact]
    public void Dbeaver_schema_prerequisite_installs_the_v3_report_contract_without_marking_ef_history()
    {
        var sql = ReadImportScript("apply-v3-report-parity-schema.sql");

        sql.Should().Contain("@target_schema IN ('LS_QA_LIENS', 'LS_LIENS')");
        sql.Should().Contain("`ClientAddressLine1` varchar(300)");
        sql.Should().Contain("`TrackingFollowUpDate` date NULL");
        sql.Should().Contain("CREATE TABLE IF NOT EXISTS `liens_LegacyFieldMigrationStates`");
        sql.Should().Contain("FK_LegacyFieldMigrationStates_ImportRun");
        sql.Should().NotContain("INSERT INTO `__EFMigrationsHistory`");
    }

    private static string ReadImportScript(string filename) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "LegacyLiensImport", filename));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "scripts", "LegacyLiensImport")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
