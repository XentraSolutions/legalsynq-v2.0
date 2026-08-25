-- SL-CORE Program 1 v3 report-field backfill (DBeaver/MySQL script)
--
-- Restores relationships and status metadata omitted by the historical core importer:
--   * SL_CASE.CASE_LAW_FIRM -> SL_CONTACT.CONTACT_ID -> case Notes metadata lawFirmId
--   * SL_CASE.CASE_MANAGER -> SL_CASE_MANAGER.CM_ID -> case Notes metadata caseManagerId
--   * SL_CASE.CASE_ACCIDENT_TYPE -> case Notes metadata accidentTypeId/type
--   * SL_CASE.CASE_STATUS -> case Notes metadata statusLabel where canonical
--     storage collapsed New/Processing or Litigation
--   * SL_LEINS_MEDICAL_INFORMATION_FACILITY -> liens_Liens.FacilityId
--   * SL_LEINS_MEDICAL_CODE -> deterministic LegacyMedicalCode servicing rows
--   * lien medical-provider references -> LegacyMedicalFacilityInfo metadata
--   * latest active SL_CASE_NOTES row -> deterministic internal last-activity row
--
-- This is intentionally an ordinary SQL script: it has no stored procedure and
-- no DELIMITER command. Run the complete file in DBeaver only with @apply = 0.
-- The checked-in .NET runner executes an apply and owns COMMIT/ROLLBACK so an
-- error, cancellation, or failed postcondition cannot leave a partial repair.
--
-- The script does not create contacts, facilities, or lookup values. It creates
-- only the dedicated last-activity crosswalks described above. It requires the
-- approved Program 1 core and contacts/facilities imports to
-- have completed first.  Missing/ambiguous mappings or existing conflicting
-- assignments block every write.

SET @tenant_id = '019f6ae6-4348-784a-aae0-f4d636f843ad';
SET @legacy_program = '1';

-- Dry-run settings.  Leave these unchanged for the first execution.
SET @apply = 0;
SET @expected_case_updates = -1;
SET @expected_lien_facility_updates = -1;
SET @expected_medical_code_inserts = -1;
SET @expected_provider_changes = -1;
SET @expected_activity_inserts = -1;

-- For an apply, run the dry-run first. Copy all five reported change counts
-- into the expected variables above, then set @apply = 1 and rerun all.

SET @target_schema = DATABASE();
SET @tenant_id = LOWER(TRIM(@tenant_id));
SET @apply = IF(@apply = 1, 1, 0);

SELECT COUNT(*) INTO @target_table_count
FROM information_schema.tables
WHERE table_schema = @target_schema
  AND table_type = 'BASE TABLE'
  AND table_name IN (
      'liens_Cases', 'liens_Contacts', 'liens_Facilities', 'liens_Liens',
      'liens_ServicingItems', 'liens_CaseNotes',
      'liens_LookupValues', 'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns',
      'liens_LegacyFieldMigrationStates'
  );

SELECT COUNT(*) INTO @source_table_count
FROM information_schema.tables
WHERE table_schema = 'SL-CORE'
  AND table_type = 'BASE TABLE'
  AND table_name IN (
      'SL_CASE', 'SL_CONTACT', 'SL_CASE_MANAGER', 'SL_ACCIDENT_TYPE',
      'SL_MEDICAL_STATUS', 'SL_LEINS_MEDICAL', 'SL_LEINS_MEDICAL_CODE',
      'SL_CASE_NOTES',
      'SL_LEINS_MEDICAL_INFORMATION_FACILITY',
      'SL_MIGRATION_SOURCE_PROVENANCE'
  );

SELECT COUNT(*) INTO @target_parity_column_count
FROM information_schema.columns
WHERE table_schema = @target_schema
  AND table_name = 'liens_Cases'
  AND column_name IN (
      'ClientAddressLine1', 'ClientCity', 'ClientState', 'ClientPostalCode',
      'IncidentState', 'CurrentMedicalStatus', 'TrackingFollowUpDate',
      'MinorComp', 'CaseDropped', 'ImportedCreatedByName'
  );

SELECT COUNT(*), MAX(r.Id), MAX(r.OrgId), MAX(r.CreatedByUserId),
       MAX(LOWER(r.SourceFingerprint))
  INTO @core_run_count, @core_run_id, @org_id, @migration_user_id,
       @source_fingerprint
FROM liens_LegacyImportRuns r
WHERE r.TenantId = @tenant_id
  AND r.SourceSystem = 'SL-CORE'
  AND BINARY r.LegacyProgram = BINARY @legacy_program
  AND r.MappingVersion = 'sl-core-core-liens-v1'
  AND r.Status = 'Completed'
  AND EXISTS (
      SELECT 1
      FROM liens_LegacyIdCrosswalks x
      WHERE x.TenantId = r.TenantId
        AND x.ImportRunId = r.Id
        AND x.SourceSystem = 'SL-CORE'
        AND x.SourceTable = 'SL_CASE'
        AND x.TargetEntity = 'Case'
  );

SELECT COUNT(*), MAX(r.Id), MAX(r.OrgId)
  INTO @contact_run_count, @contact_run_id, @contact_org_id
FROM liens_LegacyImportRuns r
WHERE r.TenantId = @tenant_id
  AND r.SourceSystem = 'SL-CORE'
  AND BINARY r.LegacyProgram = BINARY @legacy_program
  AND r.MappingVersion IN ('sl-core-contact-facility-v2', 'sl-core-contact-facility-v3')
  AND BINARY LOWER(r.SourceFingerprint) = BINARY @source_fingerprint
  AND r.Status = 'Completed'
  AND EXISTS (
      SELECT 1
      FROM liens_LegacyIdCrosswalks x
      WHERE x.TenantId = r.TenantId
        AND x.ImportRunId = r.Id
        AND x.SourceSystem = 'SL-CORE'
        AND x.SourceTable = 'SL_CASE_MANAGER'
        AND x.TargetEntity = 'Contact'
  );

SELECT COUNT(*) INTO @provenance_count
FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE` p
WHERE p.PROVENANCE_KEY = 'sl-core-current'
  AND BINARY LOWER(p.SOURCE_FINGERPRINT) = BINARY @source_fingerprint
  AND p.IMPORT_SCOPE = 'sl-core-core-liens-v1';

SET @preflight_ok =
    @target_schema IN ('LS_QA_LIENS', 'LS_LIENS')
    AND @tenant_id REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
    AND @target_table_count = 10
    AND @source_table_count = 10
    AND @target_parity_column_count = 10
    AND @core_run_count = 1
    AND @contact_run_count = 1
    AND @contact_org_id = @org_id
    AND @provenance_count = 1;

DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_relationships;

CREATE TEMPORARY TABLE tmp_sl_core_case_relationships AS
SELECT
    source_case.CASE_ID AS LegacyCaseId,
    case_x.Id AS CaseCrosswalkId,
    case_x.TargetId AS TargetCaseId,
    target_case.TenantId AS TargetCaseTenantId,
    target_case.OrgId AS TargetCaseOrgId,
    target_case.Status AS TargetCaseStatus,
    target_case.Notes AS NotesBefore,
    target_case.ClientDob AS ExistingClientDob,
    target_case.ClientAddressLine1 AS ExistingClientAddressLine1,
    target_case.ClientCity AS ExistingClientCity,
    target_case.ClientState AS ExistingClientState,
    target_case.ClientPostalCode AS ExistingClientPostalCode,
    target_case.ClientPhone AS ExistingClientPhone,
    target_case.ClientEmail AS ExistingClientEmail,
    target_case.IncidentState AS ExistingIncidentState,
    target_case.CurrentMedicalStatus AS ExistingMedicalStatus,
    target_case.TrackingFollowUpDate AS ExistingTrackingFollowUpDate,
    target_case.MinorComp AS ExistingMinorComp,
    target_case.CaseDropped AS ExistingCaseDropped,
    target_case.ImportedCreatedByName AS ExistingImportedCreatedByName,
    DATE(source_case.CASE_DOB) AS SourceClientDob,
    LEFT(NULLIF(TRIM(source_case.CASE_ADDRESS), ''), 300) AS SourceClientAddressLine1,
    LEFT(NULLIF(TRIM(source_case.CASE_CITY), ''), 100) AS SourceClientCity,
    LEFT(NULLIF(TRIM(source_case.CASE_STATE), ''), 100) AS SourceClientState,
    LEFT(NULLIF(TRIM(source_case.CASE_ZIPCODE), ''), 20) AS SourceClientPostalCode,
    LEFT(NULLIF(TRIM(source_case.CASE_PHONE), ''), 30) AS SourceClientPhone,
    LEFT(NULLIF(TRIM(source_case.CASE_EMAIL), ''), 200) AS SourceClientEmail,
    LEFT(NULLIF(TRIM(source_case.CASE_ACCIDENT_STATE), ''), 100) AS SourceIncidentState,
    LEFT(NULLIF(TRIM(source_medical_status.MS_CODE), ''), 50) AS SourceMedicalStatus,
    NULLIF(TRIM(source_case.CASE_CURRENT_MEDICAL_STATUS), '') AS SourceMedicalStatusText,
    CASE
      WHEN NULLIF(TRIM(source_case.CASE_TRACKING), '') IS NULL THEN NULL
      WHEN TRIM(source_case.CASE_TRACKING) REGEXP '^[0-9]{4}-[0-9]{2}-[0-9]{2}$'
        THEN STR_TO_DATE(TRIM(source_case.CASE_TRACKING), '%Y-%m-%d')
      WHEN TRIM(source_case.CASE_TRACKING) REGEXP '^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$'
        THEN STR_TO_DATE(TRIM(source_case.CASE_TRACKING), '%c/%e/%Y')
      ELSE NULL
    END AS SourceTrackingFollowUpDate,
    NULLIF(TRIM(source_case.CASE_TRACKING), '') AS SourceTrackingFollowUpDateText,
    CASE UPPER(TRIM(COALESCE(source_case.CASE_MINOR_COMP, '')))
      WHEN 'YES' THEN 1 WHEN 'Y' THEN 1 WHEN 'TRUE' THEN 1 WHEN '1' THEN 1
      WHEN 'NO' THEN 0 WHEN 'N' THEN 0 WHEN 'FALSE' THEN 0 WHEN '0' THEN 0
      ELSE NULL
    END AS SourceMinorComp,
    NULLIF(TRIM(source_case.CASE_MINOR_COMP), '') AS SourceMinorCompText,
    CASE UPPER(TRIM(COALESCE(source_case.CASE_DROPPED, '')))
      WHEN 'YES' THEN 1 WHEN 'Y' THEN 1 WHEN 'TRUE' THEN 1 WHEN '1' THEN 1
      WHEN 'NO' THEN 0 WHEN 'N' THEN 0 WHEN 'FALSE' THEN 0 WHEN '0' THEN 0
      ELSE NULL
    END AS SourceCaseDropped,
    NULLIF(TRIM(source_case.CASE_DROPPED), '') AS SourceCaseDroppedText,
    LEFT(NULLIF(TRIM(source_case.CASE_CREATE_BY), ''), 100) AS SourceImportedCreatedByName,
    SHA2(CAST(JSON_ARRAY(source_case.CASE_ID, source_case.CASE_DOB, source_case.CASE_ADDRESS,
        source_case.CASE_CITY, source_case.CASE_STATE, source_case.CASE_ZIPCODE,
        source_case.CASE_PHONE, source_case.CASE_EMAIL,
        source_case.CASE_ACCIDENT_STATE, source_case.CASE_CURRENT_MEDICAL_STATUS,
        source_case.CASE_TRACKING, source_case.CASE_MINOR_COMP,
        source_case.CASE_DROPPED, source_case.CASE_CREATE_BY) AS CHAR), 256) AS ParitySourceHash,
    CASE UPPER(TRIM(COALESCE(source_case.CASE_STATUS, '')))
        WHEN 'N' THEN 'New'
        WHEN 'NEW' THEN 'New'
        WHEN 'P' THEN 'Processing'
        WHEN 'PROCESSING' THEN 'Processing'
        WHEN 'LP' THEN 'Litigation'
        WHEN 'LO' THEN 'Litigation'
        WHEN 'LC' THEN 'Litigation'
        WHEN 'LITIGATION' THEN 'Litigation'
        WHEN 'LITIGATION (PENDING)' THEN 'Litigation'
        WHEN 'LITIGATION (OPEN)' THEN 'Litigation'
        WHEN 'LITIGATION (CLOSED)' THEN 'Litigation'
        ELSE NULL
    END AS SourceStatusLabel,
    CASE
        WHEN UPPER(TRIM(COALESCE(source_case.CASE_STATUS, ''))) IN ('N', 'NEW', 'P', 'PROCESSING')
          THEN 'PreDemand'
        WHEN UPPER(TRIM(COALESCE(source_case.CASE_STATUS, ''))) IN
          ('LP', 'LO', 'LC', 'LITIGATION', 'LITIGATION (PENDING)', 'LITIGATION (OPEN)', 'LITIGATION (CLOSED)')
          THEN 'InNegotiation'
        ELSE NULL
    END AS SourceCanonicalStatus,
    CASE
        WHEN source_case.CASE_LAW_FIRM IS NULL OR source_case.CASE_LAW_FIRM = 0 THEN 0
        ELSE 1
    END AS SourceLawFirmRequired,
    source_case.CASE_LAW_FIRM AS LegacyLawFirmId,
    source_law_firm.CONTACT_ID AS SourceLawFirmId,
    law_firm_x.Id AS LawFirmCrosswalkId,
    law_firm_x.TargetId AS TargetLawFirmId,
    target_law_firm.TenantId AS TargetLawFirmTenantId,
    target_law_firm.OrgId AS TargetLawFirmOrgId,
    target_law_firm.ContactType AS TargetLawFirmContactType,
    target_law_firm.ContactSubtype AS TargetLawFirmContactSubtype,
    target_law_firm.IsActive AS TargetLawFirmIsActive,
    CASE
        WHEN source_case.CASE_MANAGER IS NULL OR source_case.CASE_MANAGER = 0 THEN 0
        ELSE 1
    END AS SourceCaseManagerRequired,
    source_case.CASE_MANAGER AS LegacyCaseManagerId,
    source_manager.CM_ID AS SourceCaseManagerId,
    manager_x.Id AS ManagerCrosswalkId,
    manager_x.TargetId AS TargetCaseManagerId,
    target_manager.TenantId AS TargetManagerTenantId,
    target_manager.OrgId AS TargetManagerOrgId,
    target_manager.ContactType AS TargetManagerContactType,
    target_manager.ContactSubtype AS TargetManagerContactSubtype,
    target_manager.IsActive AS TargetManagerIsActive,
    CASE
        WHEN NULLIF(TRIM(source_case.CASE_ATTORNEY), '') IS NULL OR TRIM(source_case.CASE_ATTORNEY) = '0' THEN 0
        WHEN TRIM(source_case.CASE_ATTORNEY) REGEXP '^[0-9]+$' THEN 1
        ELSE 2
    END AS SourceAttorneyRequirement,
    NULLIF(TRIM(source_case.CASE_ATTORNEY), '') AS LegacyAttorneyText,
    source_attorney.CM_ID AS SourceAttorneyId,
    attorney_x.Id AS AttorneyCrosswalkId,
    attorney_x.TargetId AS TargetAttorneyId,
    target_attorney.TenantId AS TargetAttorneyTenantId,
    target_attorney.OrgId AS TargetAttorneyOrgId,
    target_attorney.ContactType AS TargetAttorneyContactType,
    target_attorney.ContactSubtype AS TargetAttorneyContactSubtype,
    target_attorney.IsActive AS TargetAttorneyIsActive,
    CASE
        WHEN source_case.CASE_ACCIDENT_TYPE IS NULL THEN 0
        ELSE 1
    END AS SourceAccidentTypeRequired,
    source_case.CASE_ACCIDENT_TYPE AS LegacyAccidentTypeId,
    source_accident.AT_DESCRIPTION AS LegacyAccidentTypeName,
    accident_lookup.Id AS TargetAccidentTypeId,
    accident_lookup.Name AS TargetAccidentTypeName,
    COALESCE(accident_lookup.MatchCount, 0) AS TargetAccidentTypeMatchCount,
    (CHAR_LENGTH(COALESCE(target_case.Notes, ''))
      - CHAR_LENGTH(REPLACE(COALESCE(target_case.Notes, ''), '[legacy-meta]', '')))
      / CHAR_LENGTH('[legacy-meta]') AS MetadataMarkerCount
FROM `SL-CORE`.`SL_CASE` source_case
LEFT JOIN liens_LegacyIdCrosswalks case_x
  ON case_x.TenantId = @tenant_id
 AND case_x.SourceSystem = 'SL-CORE'
 AND case_x.SourceTable = 'SL_CASE'
 AND BINARY case_x.LegacyId = BINARY CAST(source_case.CASE_ID AS CHAR)
 AND case_x.TargetEntity = 'Case'
 AND case_x.ImportRunId = @core_run_id
LEFT JOIN liens_Cases target_case
  ON target_case.Id = case_x.TargetId
LEFT JOIN `SL-CORE`.`SL_CONTACT` source_law_firm
  ON source_law_firm.CONTACT_ID = source_case.CASE_LAW_FIRM
 AND BINARY source_law_firm.CONTACT_PROGRAM = BINARY @legacy_program
 AND source_law_firm.CONTACT_TYPE = 1
 AND COALESCE(source_law_firm.CONTACT_STATUS, 'A') = 'A'
LEFT JOIN liens_LegacyIdCrosswalks law_firm_x
  ON law_firm_x.TenantId = @tenant_id
 AND law_firm_x.SourceSystem = 'SL-CORE'
 AND law_firm_x.SourceTable = 'SL_CONTACT'
 AND BINARY law_firm_x.LegacyId = BINARY CAST(source_case.CASE_LAW_FIRM AS CHAR)
 AND law_firm_x.TargetEntity = 'Contact'
 AND EXISTS (
      SELECT 1 FROM liens_LegacyImportRuns law_firm_run
      WHERE law_firm_run.Id = law_firm_x.ImportRunId
        AND law_firm_run.TenantId = @tenant_id
        AND law_firm_run.OrgId = @org_id
        AND law_firm_run.SourceSystem = 'SL-CORE'
        AND BINARY law_firm_run.LegacyProgram = BINARY @legacy_program
        AND BINARY LOWER(law_firm_run.SourceFingerprint) = BINARY @source_fingerprint
        AND law_firm_run.MappingVersion IN ('sl-core-contact-facility-v1', 'sl-core-contact-facility-v2', 'sl-core-contact-facility-v3')
        AND law_firm_run.Status = 'Completed'
  )
LEFT JOIN liens_Contacts target_law_firm
  ON target_law_firm.Id = law_firm_x.TargetId
LEFT JOIN `SL-CORE`.`SL_CASE_MANAGER` source_manager
  ON source_manager.CM_ID = source_case.CASE_MANAGER
 AND BINARY source_manager.CM_PROGRAM = BINARY @legacy_program
 AND COALESCE(source_manager.CM_STATUS, 'A') = 'A'
LEFT JOIN liens_LegacyIdCrosswalks manager_x
  ON manager_x.TenantId = @tenant_id
 AND manager_x.SourceSystem = 'SL-CORE'
 AND manager_x.SourceTable = 'SL_CASE_MANAGER'
 AND BINARY manager_x.LegacyId = BINARY CAST(source_case.CASE_MANAGER AS CHAR)
 AND manager_x.TargetEntity = 'Contact'
 AND manager_x.ImportRunId = @contact_run_id
LEFT JOIN liens_Contacts target_manager
  ON target_manager.Id = manager_x.TargetId
LEFT JOIN `SL-CORE`.`SL_CASE_MANAGER` source_attorney
  ON source_case.CASE_ATTORNEY REGEXP '^[0-9]+$'
 AND source_attorney.CM_ID = CAST(source_case.CASE_ATTORNEY AS UNSIGNED)
 AND BINARY source_attorney.CM_PROGRAM = BINARY @legacy_program
 AND COALESCE(source_attorney.CM_STATUS, 'A') = 'A'
LEFT JOIN liens_LegacyIdCrosswalks attorney_x
  ON attorney_x.TenantId = @tenant_id
 AND attorney_x.SourceSystem = 'SL-CORE'
 AND attorney_x.SourceTable = 'SL_CASE_MANAGER'
 AND BINARY attorney_x.LegacyId = BINARY TRIM(source_case.CASE_ATTORNEY)
 AND attorney_x.TargetEntity = 'Contact'
 AND attorney_x.ImportRunId = @contact_run_id
LEFT JOIN liens_Contacts target_attorney
  ON target_attorney.Id = attorney_x.TargetId
LEFT JOIN `SL-CORE`.`SL_ACCIDENT_TYPE` source_accident
  ON source_accident.AT_ID = source_case.CASE_ACCIDENT_TYPE
LEFT JOIN `SL-CORE`.`SL_MEDICAL_STATUS` source_medical_status
  ON source_case.CASE_CURRENT_MEDICAL_STATUS REGEXP '^[0-9]+$'
 AND source_medical_status.MS_ID = CAST(source_case.CASE_CURRENT_MEDICAL_STATUS AS UNSIGNED)
 AND BINARY source_medical_status.MS_PROGRAM = BINARY @legacy_program
 AND COALESCE(source_medical_status.MS_STATUS, 'A') = 'A'
LEFT JOIN (
    SELECT LOWER(TRIM(Name)) AS NormalizedName,
           MIN(Id) AS Id,
           MIN(Name) AS Name,
           COUNT(*) AS MatchCount
    FROM liens_LookupValues
    WHERE TenantId IS NULL
      AND Category = 'AccidentType'
      AND IsActive = 1
      AND IsSystem = 1
    GROUP BY LOWER(TRIM(Name))
) accident_lookup
  ON BINARY accident_lookup.NormalizedName = BINARY LOWER(TRIM(source_accident.AT_DESCRIPTION))
WHERE BINARY source_case.CASE_PROGRAM = BINARY @legacy_program
  AND COALESCE(source_case.CASE_IS_DELETED, 'N') <> 'Y';

ALTER TABLE tmp_sl_core_case_relationships
    ADD COLUMN MetadataText VARCHAR(4000) NULL,
    ADD COLUMN ExistingLawFirmId VARCHAR(100) NULL,
    ADD COLUMN ExistingLawFirmCount INT NOT NULL DEFAULT 0,
    ADD COLUMN ExistingCaseManagerId VARCHAR(100) NULL,
    ADD COLUMN ExistingCaseManagerCount INT NOT NULL DEFAULT 0,
    ADD COLUMN ExistingAttorneyId VARCHAR(100) NULL,
    ADD COLUMN ExistingAttorneyCount INT NOT NULL DEFAULT 0,
    ADD COLUMN ExistingAccidentTypeId VARCHAR(100) NULL,
    ADD COLUMN ExistingAccidentTypeIdCount INT NOT NULL DEFAULT 0,
    ADD COLUMN ExistingAccidentTypeName VARCHAR(250) NULL,
    ADD COLUMN ExistingAccidentTypeNameCount INT NOT NULL DEFAULT 0,
    ADD COLUMN ExistingStatusLabel VARCHAR(100) NULL,
    ADD COLUMN ExistingStatusLabelCount INT NOT NULL DEFAULT 0,
    ADD COLUMN BlockingReason VARCHAR(300) NULL,
    ADD COLUMN NeedsLawFirm TINYINT NOT NULL DEFAULT 0,
    ADD COLUMN NeedsCaseManager TINYINT NOT NULL DEFAULT 0,
    ADD COLUMN NeedsAttorney TINYINT NOT NULL DEFAULT 0,
    ADD COLUMN NeedsAccidentTypeId TINYINT NOT NULL DEFAULT 0,
    ADD COLUMN NeedsAccidentTypeName TINYINT NOT NULL DEFAULT 0,
    ADD COLUMN NeedsStatusLabel TINYINT NOT NULL DEFAULT 0,
    ADD COLUMN NeedsParityFields TINYINT NOT NULL DEFAULT 0,
    ADD COLUMN NotesAfter VARCHAR(4000) NULL;

UPDATE tmp_sl_core_case_relationships
SET MetadataText = CASE
    WHEN MetadataMarkerCount = 1 THEN TRIM(SUBSTRING(
        NotesBefore,
        LOCATE('[legacy-meta]', NotesBefore) + CHAR_LENGTH('[legacy-meta]')
    ))
    ELSE NULL
END;

UPDATE tmp_sl_core_case_relationships
SET ExistingLawFirmCount = CASE
        WHEN MetadataText IS NULL THEN 0
        ELSE (CHAR_LENGTH(LOWER(MetadataText))
              - CHAR_LENGTH(REPLACE(LOWER(MetadataText), 'lawfirmid=', '')))
             / CHAR_LENGTH('lawFirmId=')
    END,
    ExistingLawFirmId = NULLIF(TRIM(SUBSTRING_INDEX(
        REGEXP_SUBSTR(COALESCE(MetadataText, ''),
            '(^|;[[:space:]]*)lawFirmId=[^;[:space:]]*', 1, 1, 'i'),
        '=', -1)), ''),
    ExistingCaseManagerCount = CASE
        WHEN MetadataText IS NULL THEN 0
        ELSE (CHAR_LENGTH(LOWER(MetadataText))
              - CHAR_LENGTH(REPLACE(LOWER(MetadataText), 'casemanagerid=', '')))
             / CHAR_LENGTH('caseManagerId=')
    END,
    ExistingCaseManagerId = NULLIF(TRIM(SUBSTRING_INDEX(
        REGEXP_SUBSTR(COALESCE(MetadataText, ''),
            '(^|;[[:space:]]*)caseManagerId=[^;[:space:]]*', 1, 1, 'i'),
        '=', -1)), ''),
    ExistingAttorneyCount = CASE
        WHEN MetadataText IS NULL THEN 0
        ELSE (CHAR_LENGTH(LOWER(MetadataText))
              - CHAR_LENGTH(REPLACE(LOWER(MetadataText), 'attorneyid=', '')))
             / CHAR_LENGTH('attorneyId=')
    END,
    ExistingAttorneyId = NULLIF(TRIM(SUBSTRING_INDEX(
        REGEXP_SUBSTR(COALESCE(MetadataText, ''),
            '(^|;[[:space:]]*)attorneyId=[^;[:space:]]*', 1, 1, 'i'),
        '=', -1)), ''),
    ExistingAccidentTypeIdCount = CASE
        WHEN MetadataText IS NULL THEN 0
        ELSE (CHAR_LENGTH(LOWER(MetadataText))
              - CHAR_LENGTH(REPLACE(LOWER(MetadataText), 'accidenttypeid=', '')))
             / CHAR_LENGTH('accidentTypeId=')
    END,
    ExistingAccidentTypeId = NULLIF(TRIM(SUBSTRING_INDEX(
        REGEXP_SUBSTR(COALESCE(MetadataText, ''),
            '(^|;[[:space:]]*)accidentTypeId=[^;[:space:]]*', 1, 1, 'i'),
        '=', -1)), ''),
    ExistingAccidentTypeNameCount = CASE
        WHEN MetadataText IS NULL THEN 0
        ELSE (CHAR_LENGTH(LOWER(MetadataText))
              - CHAR_LENGTH(REPLACE(LOWER(MetadataText), 'accidenttype=', '')))
             / CHAR_LENGTH('accidentType=')
    END,
    ExistingAccidentTypeName = NULLIF(TRIM(SUBSTRING_INDEX(
        REGEXP_SUBSTR(COALESCE(MetadataText, ''),
            '(^|;[[:space:]]*)accidentType=[^;]*', 1, 1, 'i'),
        '=', -1)), ''),
    ExistingStatusLabelCount = CASE
        WHEN MetadataText IS NULL THEN 0
        ELSE (CHAR_LENGTH(LOWER(MetadataText))
              - CHAR_LENGTH(REPLACE(LOWER(MetadataText), 'statuslabel=', '')))
             / CHAR_LENGTH('statusLabel=')
    END,
    ExistingStatusLabel = NULLIF(TRIM(SUBSTRING_INDEX(
        REGEXP_SUBSTR(COALESCE(MetadataText, ''),
            '(^|;[[:space:]]*)statusLabel=[^;]*', 1, 1, 'i'),
        '=', -1)), '');

UPDATE tmp_sl_core_case_relationships
SET BlockingReason = CASE
    WHEN CaseCrosswalkId IS NULL THEN 'MissingCaseCrosswalk'
    WHEN TargetCaseId IS NULL
      OR TargetCaseTenantId <> @tenant_id
      OR TargetCaseOrgId <> @org_id THEN 'InvalidTargetCase'
    WHEN SourceLawFirmRequired = 1 AND SourceLawFirmId IS NULL
      THEN 'MissingOrInactiveSourceLawFirm'
    WHEN SourceLawFirmRequired = 1 AND LawFirmCrosswalkId IS NULL
      THEN 'MissingLawFirmCrosswalk'
    WHEN SourceLawFirmRequired = 1
      AND (TargetLawFirmId IS NULL
        OR TargetLawFirmTenantId <> @tenant_id
        OR TargetLawFirmOrgId <> @org_id
        OR TargetLawFirmContactType <> 'LawFirm'
        OR TargetLawFirmContactSubtype IS NOT NULL
        OR TargetLawFirmIsActive <> 1)
      THEN 'InvalidTargetLawFirm'
    WHEN SourceCaseManagerRequired = 1 AND SourceCaseManagerId IS NULL
      THEN 'MissingOrInactiveSourceCaseManager'
    WHEN SourceCaseManagerRequired = 1 AND ManagerCrosswalkId IS NULL
      THEN 'MissingCaseManagerCrosswalk'
    WHEN SourceCaseManagerRequired = 1
      AND (TargetCaseManagerId IS NULL
        OR TargetManagerTenantId <> @tenant_id
        OR TargetManagerOrgId <> @org_id
        OR TargetManagerContactType <> 'LawFirm'
        OR TargetManagerContactSubtype <> 'CaseManager'
        OR TargetManagerIsActive <> 1)
      THEN 'InvalidTargetCaseManager'
    WHEN SourceAttorneyRequirement = 2 THEN 'InvalidLegacyAttorneyReference'
    WHEN SourceAttorneyRequirement = 1 AND SourceAttorneyId IS NULL
      THEN 'MissingOrInactiveSourceAttorney'
    WHEN SourceAttorneyRequirement = 1 AND AttorneyCrosswalkId IS NULL
      THEN 'MissingAttorneyCrosswalk'
    WHEN SourceAttorneyRequirement = 1
      AND (TargetAttorneyId IS NULL
        OR TargetAttorneyTenantId <> @tenant_id
        OR TargetAttorneyOrgId <> @org_id
        OR TargetAttorneyContactType <> 'LawFirm'
        OR TargetAttorneyContactSubtype NOT IN ('Attorney', 'CaseManager')
        OR TargetAttorneyIsActive <> 1)
      THEN 'InvalidTargetAttorney'
    WHEN SourceAccidentTypeRequired = 1 AND LegacyAccidentTypeName IS NULL
      THEN 'MissingLegacyAccidentType'
    WHEN SourceAccidentTypeRequired = 1 AND TargetAccidentTypeMatchCount <> 1
      THEN 'MissingOrAmbiguousTargetAccidentTypeLookup'
    WHEN MetadataMarkerCount > 1 THEN 'AmbiguousLegacyMetadata'
    WHEN MetadataMarkerCount = 0
      AND TRIM(COALESCE(NotesBefore, '')) REGEXP
          '^[A-Za-z0-9_]+=[^;]+(;[[:space:]]*[A-Za-z0-9_]+=[^;]+)*$'
      AND REGEXP_LIKE(NotesBefore,
          '(^|;[[:space:]]*)(lawFirmId|caseManagerId|attorneyId|accidentTypeId|accidentType|statusLabel)=', 'i')
      THEN 'UnmarkedLegacyMetadata'
    WHEN ExistingLawFirmCount > 1 THEN 'DuplicateLawFirmMetadata'
    WHEN ExistingCaseManagerCount > 1 THEN 'DuplicateCaseManagerMetadata'
    WHEN ExistingAttorneyCount > 1 THEN 'DuplicateAttorneyMetadata'
    WHEN ExistingAccidentTypeIdCount > 1 THEN 'DuplicateAccidentTypeIdMetadata'
    WHEN ExistingAccidentTypeNameCount > 1 THEN 'DuplicateAccidentTypeMetadata'
    WHEN ExistingStatusLabelCount > 1 THEN 'DuplicateStatusLabelMetadata'
    WHEN SourceLawFirmRequired = 1
      AND ExistingLawFirmCount = 1
      AND ExistingLawFirmId IS NULL THEN 'MalformedExistingLawFirmId'
    WHEN SourceLawFirmRequired = 1
      AND ExistingLawFirmId IS NOT NULL
      AND LOWER(ExistingLawFirmId) <> LOWER(TargetLawFirmId)
      THEN 'ConflictingExistingLawFirmId'
    WHEN SourceCaseManagerRequired = 1
      AND ExistingCaseManagerCount = 1
      AND ExistingCaseManagerId IS NULL THEN 'MalformedExistingCaseManagerId'
    WHEN SourceCaseManagerRequired = 1
      AND ExistingCaseManagerId IS NOT NULL
      AND LOWER(ExistingCaseManagerId) <> LOWER(TargetCaseManagerId)
      THEN 'ConflictingExistingCaseManagerId'
    WHEN SourceAttorneyRequirement = 1
      AND ExistingAttorneyCount = 1
      AND ExistingAttorneyId IS NULL THEN 'MalformedExistingAttorneyId'
    WHEN SourceAttorneyRequirement = 1
      AND ExistingAttorneyId IS NOT NULL
      AND LOWER(ExistingAttorneyId) <> LOWER(TargetAttorneyId)
      THEN 'ConflictingExistingAttorneyId'
    WHEN SourceAccidentTypeRequired = 1
      AND ExistingAccidentTypeIdCount = 1
      AND ExistingAccidentTypeId IS NULL THEN 'MalformedExistingAccidentTypeId'
    WHEN SourceAccidentTypeRequired = 1
      AND ExistingAccidentTypeId IS NOT NULL
      AND LOWER(ExistingAccidentTypeId) <> LOWER(TargetAccidentTypeId)
      THEN 'ConflictingExistingAccidentTypeId'
    WHEN SourceAccidentTypeRequired = 1
      AND ExistingAccidentTypeNameCount = 1
      AND ExistingAccidentTypeName IS NULL THEN 'MalformedExistingAccidentType'
    WHEN SourceAccidentTypeRequired = 1
      AND ExistingAccidentTypeName IS NOT NULL
      AND LOWER(ExistingAccidentTypeName) <> LOWER(TargetAccidentTypeName)
      THEN 'ConflictingExistingAccidentType'
    WHEN SourceStatusLabel IS NOT NULL
      AND TargetCaseStatus = SourceCanonicalStatus
      AND ExistingStatusLabelCount = 1
      AND ExistingStatusLabel IS NULL THEN 'MalformedExistingStatusLabel'
    WHEN SourceStatusLabel IS NOT NULL
      AND TargetCaseStatus = SourceCanonicalStatus
      AND ExistingStatusLabel IS NOT NULL
      AND LOWER(ExistingStatusLabel) <> LOWER(SourceStatusLabel)
      THEN 'ConflictingExistingStatusLabel'
    WHEN SourceTrackingFollowUpDateText IS NOT NULL AND SourceTrackingFollowUpDate IS NULL
      THEN 'InvalidTrackingFollowUpDate'
    WHEN SourceMinorCompText IS NOT NULL AND SourceMinorComp IS NULL
      THEN 'InvalidMinorCompFlag'
    WHEN SourceCaseDroppedText IS NOT NULL AND SourceCaseDropped IS NULL
      THEN 'InvalidCaseDroppedFlag'
    WHEN SourceMedicalStatusText IS NOT NULL AND SourceMedicalStatus IS NULL
      THEN 'InvalidMedicalStatus'
    WHEN SourceClientDob IS NOT NULL AND ExistingClientDob IS NOT NULL
      AND ExistingClientDob <> SourceClientDob THEN 'ConflictingClientDob'
    WHEN SourceClientAddressLine1 IS NOT NULL AND ExistingClientAddressLine1 IS NOT NULL
      AND ExistingClientAddressLine1 <> SourceClientAddressLine1 THEN 'ConflictingClientAddressLine1'
    WHEN SourceClientCity IS NOT NULL AND ExistingClientCity IS NOT NULL
      AND ExistingClientCity <> SourceClientCity THEN 'ConflictingClientCity'
    WHEN SourceClientState IS NOT NULL AND ExistingClientState IS NOT NULL
      AND ExistingClientState <> SourceClientState THEN 'ConflictingClientState'
    WHEN SourceClientPostalCode IS NOT NULL AND ExistingClientPostalCode IS NOT NULL
      AND ExistingClientPostalCode <> SourceClientPostalCode THEN 'ConflictingClientPostalCode'
    WHEN SourceClientPhone IS NOT NULL AND ExistingClientPhone IS NOT NULL
      AND ExistingClientPhone <> SourceClientPhone THEN 'ConflictingClientPhone'
    WHEN SourceClientEmail IS NOT NULL AND ExistingClientEmail IS NOT NULL
      AND ExistingClientEmail <> SourceClientEmail THEN 'ConflictingClientEmail'
    WHEN SourceIncidentState IS NOT NULL AND ExistingIncidentState IS NOT NULL
      AND ExistingIncidentState <> SourceIncidentState THEN 'ConflictingIncidentState'
    WHEN SourceMedicalStatus IS NOT NULL AND ExistingMedicalStatus IS NOT NULL
      AND ExistingMedicalStatus <> SourceMedicalStatus THEN 'ConflictingMedicalStatus'
    WHEN SourceTrackingFollowUpDate IS NOT NULL AND ExistingTrackingFollowUpDate IS NOT NULL
      AND ExistingTrackingFollowUpDate <> SourceTrackingFollowUpDate THEN 'ConflictingTrackingFollowUpDate'
    WHEN SourceMinorComp IS NOT NULL AND ExistingMinorComp IS NOT NULL
      AND ExistingMinorComp <> SourceMinorComp THEN 'ConflictingMinorComp'
    WHEN SourceCaseDropped IS NOT NULL AND ExistingCaseDropped IS NOT NULL
      AND ExistingCaseDropped <> SourceCaseDropped THEN 'ConflictingCaseDropped'
    WHEN SourceImportedCreatedByName IS NOT NULL AND ExistingImportedCreatedByName IS NOT NULL
      AND ExistingImportedCreatedByName <> SourceImportedCreatedByName THEN 'ConflictingImportedCreatedByName'
    WHEN EXISTS (
        SELECT 1 FROM liens_LegacyFieldMigrationStates ledger
        WHERE ledger.TenantId = @tenant_id
          AND ledger.SourceSystem = 'SL-CORE'
          AND ledger.SourceTable = 'SL_CASE'
          AND BINARY ledger.LegacyId = BINARY CAST(LegacyCaseId AS CHAR)
          AND ledger.MappingVersion = 'sl-core-report-parity-v2'
          AND ledger.FieldGroup = 'CaseReportParity'
          AND (ledger.TargetEntity <> 'Case'
            OR ledger.TargetId <> TargetCaseId
            OR ledger.SourceHash <> ParitySourceHash
            OR ledger.ImportRunId <> @core_run_id
            OR ledger.Status <> 'Applied')
    ) THEN 'ConflictingParityLedger'
    WHEN EXISTS (
        SELECT 1 FROM liens_LegacyFieldMigrationStates ledger
        WHERE ledger.TenantId = @tenant_id
          AND ledger.SourceSystem = 'SL-CORE'
          AND ledger.SourceTable = 'SL_CASE'
          AND BINARY ledger.LegacyId = BINARY CAST(LegacyCaseId AS CHAR)
          AND ledger.MappingVersion = 'sl-core-report-parity-v2'
          AND ledger.FieldGroup = 'CaseReportParity'
    ) AND (
        (SourceClientAddressLine1 IS NOT NULL AND ExistingClientAddressLine1 IS NULL)
     OR (SourceClientDob IS NOT NULL AND ExistingClientDob IS NULL)
     OR (SourceClientCity IS NOT NULL AND ExistingClientCity IS NULL)
     OR (SourceClientState IS NOT NULL AND ExistingClientState IS NULL)
     OR (SourceClientPostalCode IS NOT NULL AND ExistingClientPostalCode IS NULL)
     OR (SourceClientPhone IS NOT NULL AND ExistingClientPhone IS NULL)
     OR (SourceClientEmail IS NOT NULL AND ExistingClientEmail IS NULL)
     OR (SourceIncidentState IS NOT NULL AND ExistingIncidentState IS NULL)
     OR (SourceMedicalStatus IS NOT NULL AND ExistingMedicalStatus IS NULL)
     OR (SourceTrackingFollowUpDate IS NOT NULL AND ExistingTrackingFollowUpDate IS NULL)
     OR (SourceMinorComp IS NOT NULL AND ExistingMinorComp IS NULL)
     OR (SourceCaseDropped IS NOT NULL AND ExistingCaseDropped IS NULL)
     OR (SourceImportedCreatedByName IS NOT NULL AND ExistingImportedCreatedByName IS NULL)
    ) THEN 'ParityLedgerHasIncompleteTarget'
    ELSE NULL
END;

UPDATE tmp_sl_core_case_relationships
SET NeedsLawFirm = CASE
        WHEN BlockingReason IS NULL
         AND SourceLawFirmRequired = 1
         AND ExistingLawFirmCount = 0 THEN 1 ELSE 0 END,
    NeedsCaseManager = CASE
        WHEN BlockingReason IS NULL
         AND SourceCaseManagerRequired = 1
         AND ExistingCaseManagerCount = 0 THEN 1 ELSE 0 END,
    NeedsAttorney = CASE
        WHEN BlockingReason IS NULL
         AND SourceAttorneyRequirement = 1
         AND ExistingAttorneyCount = 0 THEN 1 ELSE 0 END,
    NeedsAccidentTypeId = CASE
        WHEN BlockingReason IS NULL
         AND SourceAccidentTypeRequired = 1
         AND ExistingAccidentTypeIdCount = 0 THEN 1 ELSE 0 END,
    NeedsAccidentTypeName = CASE
        WHEN BlockingReason IS NULL
         AND SourceAccidentTypeRequired = 1
         AND ExistingAccidentTypeNameCount = 0 THEN 1 ELSE 0 END,
    NeedsStatusLabel = CASE
        WHEN BlockingReason IS NULL
         AND SourceStatusLabel IS NOT NULL
         AND TargetCaseStatus = SourceCanonicalStatus
         AND ExistingStatusLabelCount = 0 THEN 1 ELSE 0 END,
    NeedsParityFields = CASE
        WHEN BlockingReason IS NULL AND (
            (SourceClientDob IS NOT NULL AND ExistingClientDob IS NULL)
         OR (SourceClientAddressLine1 IS NOT NULL AND ExistingClientAddressLine1 IS NULL)
         OR (SourceClientCity IS NOT NULL AND ExistingClientCity IS NULL)
         OR (SourceClientState IS NOT NULL AND ExistingClientState IS NULL)
         OR (SourceClientPostalCode IS NOT NULL AND ExistingClientPostalCode IS NULL)
         OR (SourceClientPhone IS NOT NULL AND ExistingClientPhone IS NULL)
         OR (SourceClientEmail IS NOT NULL AND ExistingClientEmail IS NULL)
         OR (SourceIncidentState IS NOT NULL AND ExistingIncidentState IS NULL)
         OR (SourceMedicalStatus IS NOT NULL AND ExistingMedicalStatus IS NULL)
         OR (SourceTrackingFollowUpDate IS NOT NULL AND ExistingTrackingFollowUpDate IS NULL)
         OR (SourceMinorComp IS NOT NULL AND ExistingMinorComp IS NULL)
         OR (SourceCaseDropped IS NOT NULL AND ExistingCaseDropped IS NULL)
         OR (SourceImportedCreatedByName IS NOT NULL AND ExistingImportedCreatedByName IS NULL)
        ) THEN 1 ELSE 0 END;

UPDATE tmp_sl_core_case_relationships
SET NotesAfter = CASE
    WHEN NeedsLawFirm = 0
     AND NeedsCaseManager = 0
     AND NeedsAttorney = 0
     AND NeedsAccidentTypeId = 0
     AND NeedsAccidentTypeName = 0
     AND NeedsStatusLabel = 0 THEN NotesBefore
    WHEN NotesBefore IS NULL OR TRIM(NotesBefore) = '' THEN CONCAT(
        '[legacy-meta]', CHAR(10),
        CONCAT_WS('; ',
            CASE WHEN NeedsLawFirm = 1
                 THEN CONCAT('lawFirmId=', TargetLawFirmId) END,
            CASE WHEN NeedsCaseManager = 1
                 THEN CONCAT('caseManagerId=', TargetCaseManagerId) END,
            CASE WHEN NeedsAttorney = 1
                 THEN CONCAT('attorneyId=', TargetAttorneyId) END,
            CASE WHEN NeedsAccidentTypeId = 1
                 THEN CONCAT('accidentTypeId=', TargetAccidentTypeId) END,
            CASE WHEN NeedsAccidentTypeName = 1
                 THEN CONCAT('accidentType=', TargetAccidentTypeName) END,
            CASE WHEN NeedsStatusLabel = 1
                 THEN CONCAT('statusLabel=', SourceStatusLabel) END
        )
    )
    WHEN MetadataMarkerCount = 0 THEN CONCAT(
        NotesBefore, CHAR(10), CHAR(10), '[legacy-meta]', CHAR(10),
        CONCAT_WS('; ',
            CASE WHEN NeedsLawFirm = 1
                 THEN CONCAT('lawFirmId=', TargetLawFirmId) END,
            CASE WHEN NeedsCaseManager = 1
                 THEN CONCAT('caseManagerId=', TargetCaseManagerId) END,
            CASE WHEN NeedsAttorney = 1
                 THEN CONCAT('attorneyId=', TargetAttorneyId) END,
            CASE WHEN NeedsAccidentTypeId = 1
                 THEN CONCAT('accidentTypeId=', TargetAccidentTypeId) END,
            CASE WHEN NeedsAccidentTypeName = 1
                 THEN CONCAT('accidentType=', TargetAccidentTypeName) END,
            CASE WHEN NeedsStatusLabel = 1
                 THEN CONCAT('statusLabel=', SourceStatusLabel) END
        )
    )
    WHEN MetadataText IS NULL OR MetadataText = '' THEN CONCAT(
        NotesBefore, CHAR(10),
        CONCAT_WS('; ',
            CASE WHEN NeedsLawFirm = 1
                 THEN CONCAT('lawFirmId=', TargetLawFirmId) END,
            CASE WHEN NeedsCaseManager = 1
                 THEN CONCAT('caseManagerId=', TargetCaseManagerId) END,
            CASE WHEN NeedsAttorney = 1
                 THEN CONCAT('attorneyId=', TargetAttorneyId) END,
            CASE WHEN NeedsAccidentTypeId = 1
                 THEN CONCAT('accidentTypeId=', TargetAccidentTypeId) END,
            CASE WHEN NeedsAccidentTypeName = 1
                 THEN CONCAT('accidentType=', TargetAccidentTypeName) END,
            CASE WHEN NeedsStatusLabel = 1
                 THEN CONCAT('statusLabel=', SourceStatusLabel) END
        )
    )
    ELSE CONCAT(
        NotesBefore, '; ',
        CONCAT_WS('; ',
            CASE WHEN NeedsLawFirm = 1
                 THEN CONCAT('lawFirmId=', TargetLawFirmId) END,
            CASE WHEN NeedsCaseManager = 1
                 THEN CONCAT('caseManagerId=', TargetCaseManagerId) END,
            CASE WHEN NeedsAttorney = 1
                 THEN CONCAT('attorneyId=', TargetAttorneyId) END,
            CASE WHEN NeedsAccidentTypeId = 1
                 THEN CONCAT('accidentTypeId=', TargetAccidentTypeId) END,
            CASE WHEN NeedsAccidentTypeName = 1
                 THEN CONCAT('accidentType=', TargetAccidentTypeName) END,
            CASE WHEN NeedsStatusLabel = 1
                 THEN CONCAT('statusLabel=', SourceStatusLabel) END
        )
    )
END;

UPDATE tmp_sl_core_case_relationships
SET BlockingReason = 'NotesOverflow',
    NeedsLawFirm = 0,
    NeedsCaseManager = 0,
    NeedsAttorney = 0,
    NeedsAccidentTypeId = 0,
    NeedsAccidentTypeName = 0,
    NeedsStatusLabel = 0,
    NeedsParityFields = 0
WHERE BlockingReason IS NULL
  AND CHAR_LENGTH(COALESCE(NotesAfter, '')) > 4000;

DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_facility_relationships;

CREATE TEMPORARY TABLE tmp_sl_core_lien_facility_relationships AS
SELECT
    source_link.LMI_ID AS LegacyLienFacilityLinkId,
    source_link.LMI_LM_ID AS LegacyLienId,
    lien_x.Id AS LienCrosswalkId,
    lien_x.TargetId AS TargetLienId,
    target_lien.TenantId AS TargetLienTenantId,
    target_lien.OrgId AS TargetLienOrgId,
    target_lien.FacilityId AS ExistingFacilityId,
    facility_x.Id AS FacilityCrosswalkId,
    facility_x.TargetId AS TargetFacilityId,
    target_facility.TenantId AS TargetFacilityTenantId,
    target_facility.OrgId AS TargetFacilityOrgId,
    target_facility.IsActive AS TargetFacilityIsActive,
    link_x.Id AS FacilityLinkCrosswalkId,
    link_x.TargetId AS ExistingFacilityLinkTargetId,
    0 AS DistinctFacilityCount,
    NULL AS BlockingReason
FROM `SL-CORE`.`SL_LEINS_MEDICAL_INFORMATION_FACILITY` source_link
LEFT JOIN liens_LegacyIdCrosswalks lien_x
  ON lien_x.TenantId = @tenant_id
 AND lien_x.SourceSystem = 'SL-CORE'
 AND lien_x.SourceTable = 'SL_LEINS_MEDICAL'
 AND BINARY lien_x.LegacyId = BINARY CAST(source_link.LMI_LM_ID AS CHAR)
 AND lien_x.TargetEntity = 'Lien'
 AND lien_x.ImportRunId = @core_run_id
LEFT JOIN liens_Liens target_lien
  ON target_lien.Id = lien_x.TargetId
LEFT JOIN liens_LegacyIdCrosswalks facility_x
  ON facility_x.TenantId = @tenant_id
 AND facility_x.SourceSystem = 'SL-CORE'
 AND facility_x.SourceTable = 'SL_FACILITY'
 AND BINARY facility_x.LegacyId = BINARY CAST(source_link.LMI_FACILITY_ID AS CHAR)
 AND facility_x.TargetEntity = 'Facility'
 AND EXISTS (
      SELECT 1 FROM liens_LegacyImportRuns facility_run
      WHERE facility_run.Id = facility_x.ImportRunId
        AND facility_run.TenantId = @tenant_id
        AND facility_run.OrgId = @org_id
        AND facility_run.SourceSystem = 'SL-CORE'
        AND BINARY facility_run.LegacyProgram = BINARY @legacy_program
        AND BINARY LOWER(facility_run.SourceFingerprint) = BINARY @source_fingerprint
        AND facility_run.MappingVersion IN ('sl-core-contact-facility-v1', 'sl-core-contact-facility-v2', 'sl-core-contact-facility-v3')
        AND facility_run.Status = 'Completed'
  )
LEFT JOIN liens_Facilities target_facility
  ON target_facility.Id = facility_x.TargetId
LEFT JOIN liens_LegacyIdCrosswalks link_x
  ON link_x.TenantId = @tenant_id
 AND link_x.SourceSystem = 'SL-CORE'
 AND link_x.SourceTable = 'SL_LEINS_MEDICAL_INFORMATION_FACILITY'
 AND BINARY link_x.LegacyId = BINARY CAST(source_link.LMI_ID AS CHAR)
 AND link_x.TargetEntity = 'LienFacilityLink'
 AND EXISTS (
      SELECT 1 FROM liens_LegacyImportRuns link_run
      WHERE link_run.Id = link_x.ImportRunId
        AND link_run.TenantId = @tenant_id
        AND link_run.OrgId = @org_id
        AND link_run.SourceSystem = 'SL-CORE'
        AND BINARY link_run.LegacyProgram = BINARY @legacy_program
        AND BINARY LOWER(link_run.SourceFingerprint) = BINARY @source_fingerprint
        AND link_run.MappingVersion IN ('sl-core-contact-facility-v1', 'sl-core-contact-facility-v2', 'sl-core-contact-facility-v3')
        AND link_run.Status = 'Completed'
  )
WHERE source_link.LMI_FACILITY_ID IS NOT NULL;

UPDATE tmp_sl_core_lien_facility_relationships relation_row
INNER JOIN (
    SELECT TargetLienId, COUNT(DISTINCT TargetFacilityId) AS DistinctFacilityCount
    FROM tmp_sl_core_lien_facility_relationships
    WHERE TargetLienId IS NOT NULL
    GROUP BY TargetLienId
) duplicate_check ON duplicate_check.TargetLienId = relation_row.TargetLienId
SET relation_row.DistinctFacilityCount = duplicate_check.DistinctFacilityCount;

UPDATE tmp_sl_core_lien_facility_relationships
SET BlockingReason = CASE
    WHEN LienCrosswalkId IS NULL THEN 'MissingLienCrosswalk'
    WHEN TargetLienId IS NULL
      OR TargetLienTenantId <> @tenant_id
      OR TargetLienOrgId <> @org_id THEN 'InvalidTargetLien'
    WHEN FacilityCrosswalkId IS NULL THEN 'MissingFacilityCrosswalk'
    WHEN TargetFacilityId IS NULL
      OR TargetFacilityTenantId <> @tenant_id
      OR TargetFacilityOrgId <> @org_id
      OR TargetFacilityIsActive <> 1 THEN 'InvalidTargetFacility'
    WHEN FacilityLinkCrosswalkId IS NULL
      OR ExistingFacilityLinkTargetId <> TargetLienId THEN 'MissingOrInvalidFacilityLinkCrosswalk'
    WHEN DistinctFacilityCount > 1 THEN 'AmbiguousLegacyLienFacilities'
    WHEN ExistingFacilityId IS NOT NULL
      AND ExistingFacilityId <> TargetFacilityId THEN 'ConflictingExistingFacilityId'
    ELSE NULL
END;

DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_facility_updates;

CREATE TEMPORARY TABLE tmp_sl_core_lien_facility_updates AS
SELECT TargetLienId, MIN(TargetFacilityId) AS TargetFacilityId
FROM tmp_sl_core_lien_facility_relationships
WHERE BlockingReason IS NULL
  AND ExistingFacilityId IS NULL
GROUP BY TargetLienId
HAVING COUNT(DISTINCT TargetFacilityId) = 1;

DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_report_medical_codes;

CREATE TEMPORARY TABLE tmp_sl_core_report_medical_codes AS
SELECT staged.*,
       existing_task.Id AS ExistingTaskId,
       existing_task.OrgId AS ExistingTaskOrgId,
       existing_task.TaskType AS ExistingTaskType,
       existing_task.CaseId AS ExistingTaskCaseId,
       existing_task.LienId AS ExistingTaskLienId,
       existing_task.Notes AS ExistingTaskNotes,
       CASE
         WHEN staged.LienCrosswalkId IS NULL THEN 'MissingLienCrosswalk'
         WHEN staged.TargetLienId IS NULL OR staged.TargetCaseId IS NULL
           OR staged.TargetLienTenantId <> @tenant_id OR staged.TargetLienOrgId <> @org_id
           THEN 'InvalidTargetLien'
         WHEN staged.InvalidAmount = 1 THEN 'InvalidMedicalCodeAmount'
         WHEN existing_task.Id IS NULL THEN 'NeedsInsert'
         WHEN existing_task.OrgId <> @org_id
           OR existing_task.TaskType <> 'LegacyMedicalCode'
           OR NOT (existing_task.CaseId <=> staged.TargetCaseId)
           OR NOT (existing_task.LienId <=> staged.TargetLienId)
           OR BINARY COALESCE(existing_task.Notes, '') <> BINARY staged.ExpectedNotes
           THEN 'ConflictingExistingMedicalCodeTask'
         ELSE 'AlreadyCorrect'
       END AS Resolution
FROM (
    SELECT
        source_code.LMC_ID AS LegacyMedicalCodeId,
        lien_x.Id AS LienCrosswalkId,
        lien_x.TargetId AS TargetLienId,
        target_lien.CaseId AS TargetCaseId,
        target_lien.TenantId AS TargetLienTenantId,
        target_lien.OrgId AS TargetLienOrgId,
        CONCAT('SLCORE-LMC-', source_code.LMC_ID) AS TaskNumber,
        NULLIF(REPLACE(REPLACE(TRIM(source_code.LMC_CODE), ';', ' '), '=', ' '), '') AS SafeCode,
        CASE
          WHEN (NULLIF(TRIM(REPLACE(REPLACE(CAST(source_code.LMC_BILLING_AMOUNT AS CHAR), ',', ''), '$', '')), '') IS NOT NULL
            AND TRIM(REPLACE(REPLACE(CAST(source_code.LMC_BILLING_AMOUNT AS CHAR), ',', ''), '$', '')) NOT REGEXP '^-?[0-9]+([.][0-9]{1,2})?$')
            OR (NULLIF(TRIM(REPLACE(REPLACE(CAST(source_code.LMC_PURCHASE_AMOUNT AS CHAR), ',', ''), '$', '')), '') IS NOT NULL
            AND TRIM(REPLACE(REPLACE(CAST(source_code.LMC_PURCHASE_AMOUNT AS CHAR), ',', ''), '$', '')) NOT REGEXP '^-?[0-9]+([.][0-9]{1,2})?$')
          THEN 1 ELSE 0
        END AS InvalidAmount,
        CONCAT(
          'legacySource=SL-CORE:SL_LEINS_MEDICAL_CODE:', source_code.LMC_ID, '; ',
          'code=', COALESCE(NULLIF(REPLACE(REPLACE(TRIM(source_code.LMC_CODE), ';', ' '), '=', ' '), ''), ''), '; ',
          'description=; ',
          'medicareCost=', CASE
              WHEN TRIM(REPLACE(REPLACE(CAST(source_code.LMC_MEDICARE_COST AS CHAR), ',', ''), '$', '')) REGEXP '^-?[0-9]+([.][0-9]{1,2})?$'
              THEN CAST(CAST(TRIM(REPLACE(REPLACE(CAST(source_code.LMC_MEDICARE_COST AS CHAR), ',', ''), '$', '')) AS DECIMAL(20,2)) AS CHAR)
              ELSE '' END, '; ',
          'billingAmount=', CASE
              WHEN NULLIF(TRIM(REPLACE(REPLACE(CAST(source_code.LMC_BILLING_AMOUNT AS CHAR), ',', ''), '$', '')), '') IS NULL
              THEN CAST(CAST(0 AS DECIMAL(20,2)) AS CHAR)
              WHEN TRIM(REPLACE(REPLACE(CAST(source_code.LMC_BILLING_AMOUNT AS CHAR), ',', ''), '$', '')) REGEXP '^-?[0-9]+([.][0-9]{1,2})?$'
              THEN CAST(CAST(TRIM(REPLACE(REPLACE(CAST(source_code.LMC_BILLING_AMOUNT AS CHAR), ',', ''), '$', '')) AS DECIMAL(20,2)) AS CHAR)
              ELSE CAST(CAST(0 AS DECIMAL(20,2)) AS CHAR) END, '; ',
          'purchaseAmount=', CASE
              WHEN NULLIF(TRIM(REPLACE(REPLACE(CAST(source_code.LMC_PURCHASE_AMOUNT AS CHAR), ',', ''), '$', '')), '') IS NULL
              THEN CAST(CAST(0 AS DECIMAL(20,2)) AS CHAR)
              WHEN TRIM(REPLACE(REPLACE(CAST(source_code.LMC_PURCHASE_AMOUNT AS CHAR), ',', ''), '$', '')) REGEXP '^-?[0-9]+([.][0-9]{1,2})?$'
              THEN CAST(CAST(TRIM(REPLACE(REPLACE(CAST(source_code.LMC_PURCHASE_AMOUNT AS CHAR), ',', ''), '$', '')) AS DECIMAL(20,2)) AS CHAR)
              ELSE CAST(CAST(0 AS DECIMAL(20,2)) AS CHAR) END, '; ',
          'payee=; outboundCheckNumber=') AS ExpectedNotes
    FROM `SL-CORE`.`SL_LEINS_MEDICAL_CODE` source_code
    INNER JOIN `SL-CORE`.`SL_LEINS_MEDICAL` source_lien
      ON source_lien.LM_ID = source_code.LMC_LM_ID
     AND COALESCE(source_lien.LM_IS_DELETED, 'N') <> 'Y'
     AND BINARY CAST(source_lien.LM_PROGRAM AS CHAR) = BINARY @legacy_program
    LEFT JOIN liens_LegacyIdCrosswalks lien_x
      ON lien_x.TenantId = @tenant_id
     AND lien_x.SourceSystem = 'SL-CORE'
     AND lien_x.SourceTable = 'SL_LEINS_MEDICAL'
     AND BINARY lien_x.LegacyId = BINARY CAST(source_code.LMC_LM_ID AS CHAR)
     AND lien_x.TargetEntity = 'Lien'
     AND lien_x.ImportRunId = @core_run_id
    LEFT JOIN liens_Liens target_lien ON target_lien.Id = lien_x.TargetId
    WHERE UPPER(TRIM(COALESCE(source_code.LMC_STATUS, ''))) = 'A'
) staged
LEFT JOIN liens_ServicingItems existing_task
  ON existing_task.TenantId = @tenant_id
 AND BINARY existing_task.TaskNumber = BINARY staged.TaskNumber;

DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_report_providers;

CREATE TEMPORARY TABLE tmp_sl_core_report_providers AS
SELECT staged.*,
       CASE
         WHEN staged.LienCrosswalkId IS NULL THEN 'MissingLienCrosswalk'
         WHEN staged.TargetLienId IS NULL OR staged.TargetCaseId IS NULL
           OR staged.TargetLienTenantId <> @tenant_id OR staged.TargetLienOrgId <> @org_id
           THEN 'InvalidTargetLien'
         WHEN staged.LegacyProviderCount > 1 THEN 'AmbiguousLegacyMedicalProvidersForLien'
         WHEN staged.SourceProviderId IS NULL THEN 'MissingOrInactiveSourceMedicalProvider'
         WHEN staged.ProviderCrosswalkId IS NULL THEN 'MissingMedicalProviderCrosswalk'
         WHEN staged.TargetProviderId IS NULL OR staged.TargetProviderTenantId <> @tenant_id
           OR staged.TargetProviderOrgId <> @org_id OR staged.TargetProviderType <> 'Provider'
           OR staged.TargetProviderIsActive <> 1 THEN 'InvalidTargetMedicalProvider'
         WHEN staged.ExistingTaskCount > 1 THEN 'AmbiguousTargetMedicalFacilityInfoTasks'
         WHEN staged.ExistingTaskCount = 1 AND staged.ExistingTaskOrgId <> @org_id
           THEN 'InvalidTargetMedicalFacilityInfoTask'
         WHEN staged.TaskNumberOwnerId IS NOT NULL
           AND (staged.ExistingTaskId IS NULL OR staged.TaskNumberOwnerId <> staged.ExistingTaskId)
           THEN 'TaskNumberCollision'
         WHEN staged.ExistingProviderCount > 1 THEN 'DuplicateMedicalProviderMetadata'
         WHEN staged.ExistingProviderCount = 1 AND staged.ExistingProviderId IS NULL
           THEN 'MalformedExistingMedicalProviderId'
         WHEN staged.ExistingProviderId IS NOT NULL
           AND LOWER(staged.ExistingProviderId) <> LOWER(staged.TargetProviderId)
           THEN 'ConflictingExistingMedicalProviderId'
         WHEN staged.ExistingProviderId IS NOT NULL THEN 'AlreadyCorrect'
         WHEN staged.ExistingTaskCount = 0 THEN 'NeedsInsert'
         WHEN CHAR_LENGTH(staged.NotesAfter) > 4000 THEN 'NotesOverflow'
         ELSE 'NeedsUpdate'
       END AS Resolution
FROM (
    SELECT provider_source.*,
           source_provider.CONTACT_ID AS SourceProviderId,
           provider_x.Id AS ProviderCrosswalkId,
           provider_x.TargetId AS TargetProviderId,
           target_provider.TenantId AS TargetProviderTenantId,
           target_provider.OrgId AS TargetProviderOrgId,
           target_provider.ContactType AS TargetProviderType,
           target_provider.IsActive AS TargetProviderIsActive,
           COALESCE(existing_task.TaskCount, 0) AS ExistingTaskCount,
           existing_task.TaskId AS ExistingTaskId,
           existing_task.OrgId AS ExistingTaskOrgId,
           existing_task.Notes AS ExistingNotes,
           (CHAR_LENGTH(LOWER(COALESCE(existing_task.Notes, '')))
             - CHAR_LENGTH(REPLACE(LOWER(COALESCE(existing_task.Notes, '')), 'medicalproviderid=', '')))
             / CHAR_LENGTH('medicalProviderId=') AS ExistingProviderCount,
           NULLIF(SUBSTRING(
             REGEXP_SUBSTR(COALESCE(existing_task.Notes, ''), 'medicalProviderId=[0-9A-Fa-f-]{36}'),
             CHAR_LENGTH('medicalProviderId=') + 1), '') AS ExistingProviderId,
           task_owner.Id AS TaskNumberOwnerId,
           CASE
             WHEN existing_task.Notes IS NULL OR TRIM(existing_task.Notes) = ''
               THEN CONCAT('medicalProviderId=', provider_x.TargetId)
             ELSE CONCAT(existing_task.Notes, '; medicalProviderId=', provider_x.TargetId)
           END AS NotesAfter
    FROM (
        SELECT
            source_lien.LM_ID AS LegacyLienId,
            COUNT(DISTINCT NULLIF(NULLIF(TRIM(CAST(source_info.LMI_MEDICAL_PROVIDER AS CHAR)), ''), '0')) AS LegacyProviderCount,
            MIN(NULLIF(NULLIF(TRIM(CAST(source_info.LMI_MEDICAL_PROVIDER AS CHAR)), ''), '0')) AS LegacyProviderId,
            lien_x.Id AS LienCrosswalkId,
            lien_x.TargetId AS TargetLienId,
            target_lien.CaseId AS TargetCaseId,
            target_lien.TenantId AS TargetLienTenantId,
            target_lien.OrgId AS TargetLienOrgId,
            CONCAT('SLCORE-LMFI-', source_lien.LM_ID) AS TaskNumber
        FROM `SL-CORE`.`SL_LEINS_MEDICAL` source_lien
        INNER JOIN `SL-CORE`.`SL_LEINS_MEDICAL_INFORMATION_FACILITY` source_info
          ON source_info.LMI_LM_ID = source_lien.LM_ID
        LEFT JOIN liens_LegacyIdCrosswalks lien_x
          ON lien_x.TenantId = @tenant_id
         AND lien_x.SourceSystem = 'SL-CORE'
         AND lien_x.SourceTable = 'SL_LEINS_MEDICAL'
         AND BINARY lien_x.LegacyId = BINARY CAST(source_lien.LM_ID AS CHAR)
         AND lien_x.TargetEntity = 'Lien'
         AND lien_x.ImportRunId = @core_run_id
        LEFT JOIN liens_Liens target_lien ON target_lien.Id = lien_x.TargetId
        WHERE COALESCE(source_lien.LM_IS_DELETED, 'N') <> 'Y'
          AND BINARY CAST(source_lien.LM_PROGRAM AS CHAR) = BINARY @legacy_program
          AND NULLIF(NULLIF(TRIM(CAST(source_info.LMI_MEDICAL_PROVIDER AS CHAR)), ''), '0') IS NOT NULL
        GROUP BY source_lien.LM_ID, lien_x.Id, lien_x.TargetId,
                 target_lien.CaseId, target_lien.TenantId, target_lien.OrgId
    ) provider_source
    LEFT JOIN `SL-CORE`.`SL_CONTACT` source_provider
      ON source_provider.CONTACT_ID = provider_source.LegacyProviderId
     AND BINARY source_provider.CONTACT_PROGRAM = BINARY @legacy_program
     AND source_provider.CONTACT_TYPE = 2
     AND COALESCE(source_provider.CONTACT_STATUS, 'A') = 'A'
    LEFT JOIN liens_LegacyIdCrosswalks provider_x
      ON provider_x.TenantId = @tenant_id
     AND provider_x.SourceSystem = 'SL-CORE'
     AND provider_x.SourceTable = 'SL_CONTACT'
     AND BINARY provider_x.LegacyId = BINARY provider_source.LegacyProviderId
     AND provider_x.TargetEntity = 'Contact'
     AND EXISTS (
          SELECT 1 FROM liens_LegacyImportRuns provider_run
          WHERE provider_run.Id = provider_x.ImportRunId
            AND provider_run.TenantId = @tenant_id
            AND provider_run.OrgId = @org_id
            AND provider_run.SourceSystem = 'SL-CORE'
            AND BINARY provider_run.LegacyProgram = BINARY @legacy_program
            AND BINARY LOWER(provider_run.SourceFingerprint) = BINARY @source_fingerprint
            AND provider_run.MappingVersion IN ('sl-core-contact-facility-v1', 'sl-core-contact-facility-v2', 'sl-core-contact-facility-v3')
            AND provider_run.Status = 'Completed'
      )
    LEFT JOIN liens_Contacts target_provider ON target_provider.Id = provider_x.TargetId
    LEFT JOIN (
        SELECT LienId, COUNT(*) AS TaskCount, MIN(Id) AS TaskId,
               MIN(OrgId) AS OrgId, MIN(Notes) AS Notes
        FROM liens_ServicingItems
        WHERE TenantId = @tenant_id AND TaskType = 'LegacyMedicalFacilityInfo'
        GROUP BY LienId
    ) existing_task ON existing_task.LienId = provider_source.TargetLienId
    LEFT JOIN liens_ServicingItems task_owner
      ON task_owner.TenantId = @tenant_id
     AND BINARY task_owner.TaskNumber = BINARY provider_source.TaskNumber
) staged;

DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_report_activities;

CREATE TEMPORARY TABLE tmp_sl_core_report_activities AS
SELECT staged.*,
       COALESCE(activity_x.TargetId, UUID()) AS TargetActivityId,
       activity_x.Id AS ExistingCrosswalkId,
       activity_x.TargetId AS ExistingActivityId,
       existing_activity.TenantId AS ExistingActivityTenantId,
       existing_activity.CaseId AS ExistingActivityCaseId,
       existing_activity.Content AS ExistingActivityContent,
       existing_activity.Category AS ExistingActivityCategory,
       existing_activity.IsDeleted AS ExistingActivityIsDeleted,
       existing_activity.CreatedAtUtc AS ExistingActivityCreatedAtUtc,
       existing_activity.UpdatedAtUtc AS ExistingActivityUpdatedAtUtc,
       CASE
         WHEN staged.CaseCrosswalkId IS NULL OR staged.TargetCaseId IS NULL
           OR staged.TargetCaseTenantId <> @tenant_id OR staged.TargetCaseOrgId <> @org_id
           THEN 'InvalidTargetCase'
         WHEN activity_x.Id IS NULL THEN 'NeedsInsert'
         WHEN existing_activity.Id IS NULL OR existing_activity.TenantId <> @tenant_id
           OR existing_activity.CaseId <> staged.TargetCaseId
           OR BINARY existing_activity.Content <> BINARY staged.ActivityContent
           OR existing_activity.Category <> 'internal' OR existing_activity.IsDeleted <> 0
           OR existing_activity.CreatedAtUtc <> staged.ActivityAtUtc
           OR existing_activity.UpdatedAtUtc IS NOT NULL
           OR BINARY activity_x.SourceHash <> BINARY staged.SourceHash
           OR activity_x.ImportRunId <> @core_run_id
           THEN 'ConflictingExistingLastActivity'
         ELSE 'AlreadyCorrect'
       END AS Resolution
FROM (
    SELECT latest_note.CN_ID AS LegacyNoteId,
           latest_note.CN_CASE_ID AS LegacyCaseId,
           case_x.Id AS CaseCrosswalkId,
           case_x.TargetId AS TargetCaseId,
           target_case.TenantId AS TargetCaseTenantId,
           target_case.OrgId AS TargetCaseOrgId,
           TRIM(latest_note.CN_NOTE) AS ActivityContent,
           COALESCE(latest_note.CN_CREATED, target_case.CreatedAtUtc) AS ActivityAtUtc,
           LEFT(COALESCE(NULLIF(TRIM(latest_note.CN_CREATED_BY), ''), 'Legacy SL-CORE'), 250) AS ActivityCreatedByName,
           SHA2(CONCAT_WS('|', latest_note.CN_ID, latest_note.CN_CASE_ID,
               latest_note.CN_NOTE, latest_note.CN_CREATED, latest_note.CN_CREATED_BY,
               latest_note.CN_IS_DELETED, @source_fingerprint), 256) AS SourceHash
    FROM (
        SELECT source_note.*,
               ROW_NUMBER() OVER (
                   PARTITION BY source_note.CN_CASE_ID
                   ORDER BY source_note.CN_CREATED DESC, source_note.CN_ID DESC) AS RowNumber
        FROM `SL-CORE`.`SL_CASE_NOTES` source_note
        INNER JOIN `SL-CORE`.`SL_CASE` source_case
          ON source_case.CASE_ID = source_note.CN_CASE_ID
         AND BINARY source_case.CASE_PROGRAM = BINARY @legacy_program
         AND COALESCE(source_case.CASE_IS_DELETED, 'N') <> 'Y'
        WHERE COALESCE(source_note.CN_IS_DELETED, 'N') <> 'Y'
          AND NULLIF(TRIM(source_note.CN_NOTE), '') IS NOT NULL
    ) latest_note
    LEFT JOIN liens_LegacyIdCrosswalks case_x
      ON case_x.TenantId = @tenant_id
     AND case_x.SourceSystem = 'SL-CORE'
     AND case_x.SourceTable = 'SL_CASE'
     AND BINARY case_x.LegacyId = BINARY CAST(latest_note.CN_CASE_ID AS CHAR)
     AND case_x.TargetEntity = 'Case'
     AND case_x.ImportRunId = @core_run_id
    LEFT JOIN liens_Cases target_case ON target_case.Id = case_x.TargetId
    WHERE latest_note.RowNumber = 1
) staged
LEFT JOIN liens_LegacyIdCrosswalks activity_x
  ON activity_x.TenantId = @tenant_id
 AND activity_x.SourceSystem = 'SL-CORE'
 AND activity_x.SourceTable = 'SL_CASE_NOTES_LAST_ACTIVITY'
 AND BINARY activity_x.LegacyId = BINARY CAST(staged.LegacyCaseId AS CHAR)
 AND activity_x.TargetEntity = 'CaseNote'
LEFT JOIN liens_CaseNotes existing_activity ON existing_activity.Id = activity_x.TargetId;

SELECT COUNT(*) INTO @case_source_count
FROM tmp_sl_core_case_relationships;
SELECT COUNT(*) INTO @case_conflict_count
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NOT NULL;
SELECT COUNT(*) INTO @case_update_count
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NULL
  AND (NeedsCaseManager = 1
    OR NeedsLawFirm = 1
    OR NeedsAttorney = 1
    OR NeedsAccidentTypeId = 1
    OR NeedsAccidentTypeName = 1
    OR NeedsStatusLabel = 1
    OR NeedsParityFields = 1);
SELECT COUNT(*) INTO @case_manager_updates
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NULL AND NeedsCaseManager = 1;
SELECT COUNT(*) INTO @law_firm_updates
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NULL AND NeedsLawFirm = 1;
SELECT COUNT(*) INTO @accident_type_updates
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NULL AND NeedsAccidentTypeId = 1;
SELECT COUNT(*) INTO @status_label_updates
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NULL AND NeedsStatusLabel = 1;
SELECT COUNT(*) INTO @parity_field_updates
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NULL AND NeedsParityFields = 1;
SELECT COUNT(*) INTO @facility_conflict_count
FROM tmp_sl_core_lien_facility_relationships
WHERE BlockingReason IS NOT NULL;
SELECT COUNT(*) INTO @facility_update_count
FROM tmp_sl_core_lien_facility_updates;
SELECT COUNT(*) INTO @medical_code_insert_count
FROM tmp_sl_core_report_medical_codes
WHERE Resolution = 'NeedsInsert';
SELECT COUNT(*) INTO @medical_code_conflict_count
FROM tmp_sl_core_report_medical_codes
WHERE Resolution NOT IN ('NeedsInsert', 'AlreadyCorrect');
SELECT COUNT(*) INTO @provider_insert_count
FROM tmp_sl_core_report_providers
WHERE Resolution = 'NeedsInsert';
SELECT COUNT(*) INTO @provider_update_count
FROM tmp_sl_core_report_providers
WHERE Resolution = 'NeedsUpdate';
SET @provider_change_count = @provider_insert_count + @provider_update_count;
SELECT COUNT(*) INTO @provider_conflict_count
FROM tmp_sl_core_report_providers
WHERE Resolution NOT IN ('NeedsInsert', 'NeedsUpdate', 'AlreadyCorrect');
SELECT COUNT(*) INTO @activity_insert_count
FROM tmp_sl_core_report_activities
WHERE Resolution = 'NeedsInsert';
SELECT COUNT(*) INTO @activity_conflict_count
FROM tmp_sl_core_report_activities
WHERE Resolution NOT IN ('NeedsInsert', 'AlreadyCorrect');

SET @apply_permitted =
    @apply = 1
    AND @preflight_ok = 1
    AND @case_conflict_count = 0
    AND @facility_conflict_count = 0
    AND @medical_code_conflict_count = 0
    AND @provider_conflict_count = 0
    AND @activity_conflict_count = 0
    AND @expected_case_updates = @case_update_count
    AND @expected_lien_facility_updates = @facility_update_count
    AND @expected_medical_code_inserts = @medical_code_insert_count
    AND @expected_provider_changes = @provider_change_count
    AND @expected_activity_inserts = @activity_insert_count;

SELECT
    @target_schema AS TargetSchema,
    @tenant_id AS TenantId,
    @org_id AS OrgIdFromCompletedCoreImport,
    @core_run_id AS CoreImportRunId,
    @contact_run_id AS ContactFacilityImportRunId,
    @preflight_ok AS PreflightPassed,
    @apply AS ApplyRequested,
    @apply_permitted AS ApplyPermitted,
    @case_source_count AS SourceCases,
    @case_update_count AS CaseRowsToUpdate,
    @law_firm_updates AS LawFirmRelationsToAdd,
    @case_manager_updates AS CaseManagerRelationsToAdd,
    @accident_type_updates AS AccidentTypeRelationsToAdd,
    @status_label_updates AS CaseStatusLabelsToAdd,
    @parity_field_updates AS CaseReportParityRowsToAdd,
    @facility_update_count AS LienFacilityRowsToUpdate,
    @medical_code_insert_count AS MedicalCodeRowsToInsert,
    @provider_change_count AS MedicalProviderRowsToChange,
    @activity_insert_count AS LastActivityRowsToInsert,
    @case_conflict_count AS CaseConflicts,
    @facility_conflict_count AS FacilityConflicts,
    @medical_code_conflict_count AS MedicalCodeConflicts,
    @provider_conflict_count AS MedicalProviderConflicts,
    @activity_conflict_count AS LastActivityConflicts;

SELECT BlockingReason, COUNT(*) AS AffectedCases
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NOT NULL
GROUP BY BlockingReason
ORDER BY BlockingReason;

SELECT BlockingReason, COUNT(*) AS AffectedLienFacilityLinks
FROM tmp_sl_core_lien_facility_relationships
WHERE BlockingReason IS NOT NULL
GROUP BY BlockingReason
ORDER BY BlockingReason;

SELECT Resolution, COUNT(*) AS AffectedMedicalCodeRows
FROM tmp_sl_core_report_medical_codes
WHERE Resolution NOT IN ('NeedsInsert', 'AlreadyCorrect')
GROUP BY Resolution
ORDER BY Resolution;

SELECT Resolution, COUNT(*) AS AffectedMedicalProviderRows
FROM tmp_sl_core_report_providers
WHERE Resolution NOT IN ('NeedsInsert', 'NeedsUpdate', 'AlreadyCorrect')
GROUP BY Resolution
ORDER BY Resolution;

SELECT Resolution, COUNT(*) AS AffectedLastActivityRows
FROM tmp_sl_core_report_activities
WHERE Resolution NOT IN ('NeedsInsert', 'AlreadyCorrect')
GROUP BY Resolution
ORDER BY Resolution;

SELECT LegacyCaseId, TargetCaseId, LegacyLawFirmId, LegacyCaseManagerId,
       LegacyAccidentTypeId, LegacyAccidentTypeName, TargetLawFirmId,
       TargetCaseManagerId, TargetAccidentTypeId,
       BlockingReason
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NOT NULL
ORDER BY LegacyCaseId
LIMIT 100;

-- The guarded transaction changes nothing unless every preflight condition is
-- valid, there are zero conflicts, @apply is 1, and all expected counts match.
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
START TRANSACTION;

SELECT COUNT(*) INTO @case_preimage_matches
FROM liens_Cases target_case
INNER JOIN tmp_sl_core_case_relationships staged
  ON staged.TargetCaseId = target_case.Id
WHERE @apply_permitted = 1
  AND staged.BlockingReason IS NULL
  AND (staged.NeedsLawFirm = 1
    OR staged.NeedsCaseManager = 1
    OR staged.NeedsAttorney = 1
    OR staged.NeedsAccidentTypeId = 1
    OR staged.NeedsAccidentTypeName = 1
    OR staged.NeedsStatusLabel = 1
    OR staged.NeedsParityFields = 1)
  AND target_case.TenantId = @tenant_id
  AND target_case.OrgId = @org_id
  AND target_case.Notes <=> staged.NotesBefore
  AND target_case.ClientDob <=> staged.ExistingClientDob
  AND target_case.ClientAddressLine1 <=> staged.ExistingClientAddressLine1
  AND target_case.ClientCity <=> staged.ExistingClientCity
  AND target_case.ClientState <=> staged.ExistingClientState
  AND target_case.ClientPostalCode <=> staged.ExistingClientPostalCode
  AND target_case.ClientPhone <=> staged.ExistingClientPhone
  AND target_case.ClientEmail <=> staged.ExistingClientEmail
  AND target_case.IncidentState <=> staged.ExistingIncidentState
  AND target_case.CurrentMedicalStatus <=> staged.ExistingMedicalStatus
  AND target_case.TrackingFollowUpDate <=> staged.ExistingTrackingFollowUpDate
  AND target_case.MinorComp <=> staged.ExistingMinorComp
  AND target_case.CaseDropped <=> staged.ExistingCaseDropped
  AND target_case.ImportedCreatedByName <=> staged.ExistingImportedCreatedByName
FOR UPDATE;

SELECT COUNT(*) INTO @facility_preimage_matches
FROM liens_Liens target_lien
INNER JOIN tmp_sl_core_lien_facility_updates staged
  ON staged.TargetLienId = target_lien.Id
WHERE @apply_permitted = 1
  AND target_lien.TenantId = @tenant_id
  AND target_lien.OrgId = @org_id
  AND target_lien.FacilityId IS NULL
FOR UPDATE;

SELECT COUNT(*) INTO @provider_update_preimage_matches
FROM liens_ServicingItems target_task
INNER JOIN tmp_sl_core_report_providers staged
  ON staged.ExistingTaskId = target_task.Id
WHERE @apply_permitted = 1
  AND staged.Resolution = 'NeedsUpdate'
  AND target_task.TenantId = @tenant_id
  AND target_task.OrgId = @org_id
  AND target_task.Notes <=> staged.ExistingNotes
FOR UPDATE;

SELECT COUNT(*) INTO @medical_code_insert_preimage_matches
FROM tmp_sl_core_report_medical_codes staged
LEFT JOIN liens_ServicingItems target_task
  ON target_task.TenantId = @tenant_id
 AND BINARY target_task.TaskNumber = BINARY staged.TaskNumber
WHERE @apply_permitted = 1
  AND staged.Resolution = 'NeedsInsert'
  AND target_task.Id IS NULL;

SELECT COUNT(*) INTO @provider_insert_preimage_matches
FROM tmp_sl_core_report_providers staged
LEFT JOIN liens_ServicingItems target_task
  ON target_task.TenantId = @tenant_id
 AND BINARY target_task.TaskNumber = BINARY staged.TaskNumber
WHERE @apply_permitted = 1
  AND staged.Resolution = 'NeedsInsert'
  AND target_task.Id IS NULL;

SELECT COUNT(*) INTO @activity_insert_preimage_matches
FROM tmp_sl_core_report_activities staged
LEFT JOIN liens_LegacyIdCrosswalks target_x
  ON target_x.TenantId = @tenant_id
 AND target_x.SourceSystem = 'SL-CORE'
 AND target_x.SourceTable = 'SL_CASE_NOTES_LAST_ACTIVITY'
 AND BINARY target_x.LegacyId = BINARY CAST(staged.LegacyCaseId AS CHAR)
 AND target_x.TargetEntity = 'CaseNote'
WHERE @apply_permitted = 1
  AND staged.Resolution = 'NeedsInsert'
  AND target_x.Id IS NULL;

SET @apply_permitted =
    @apply_permitted = 1
    AND @case_preimage_matches = @case_update_count
    AND @facility_preimage_matches = @facility_update_count
    AND @medical_code_insert_preimage_matches = @medical_code_insert_count
    AND @provider_insert_preimage_matches = @provider_insert_count
    AND @provider_update_preimage_matches = @provider_update_count
    AND @activity_insert_preimage_matches = @activity_insert_count;

UPDATE liens_Cases target_case
INNER JOIN tmp_sl_core_case_relationships staged
  ON staged.TargetCaseId = target_case.Id
SET target_case.Notes = staged.NotesAfter,
    target_case.ClientDob = COALESCE(target_case.ClientDob, staged.SourceClientDob),
    target_case.ClientAddressLine1 = COALESCE(target_case.ClientAddressLine1, staged.SourceClientAddressLine1),
    target_case.ClientCity = COALESCE(target_case.ClientCity, staged.SourceClientCity),
    target_case.ClientState = COALESCE(target_case.ClientState, staged.SourceClientState),
    target_case.ClientPostalCode = COALESCE(target_case.ClientPostalCode, staged.SourceClientPostalCode),
    target_case.ClientPhone = COALESCE(target_case.ClientPhone, staged.SourceClientPhone),
    target_case.ClientEmail = COALESCE(target_case.ClientEmail, staged.SourceClientEmail),
    target_case.IncidentState = COALESCE(target_case.IncidentState, staged.SourceIncidentState),
    target_case.CurrentMedicalStatus = COALESCE(target_case.CurrentMedicalStatus, staged.SourceMedicalStatus),
    target_case.TrackingFollowUpDate = COALESCE(target_case.TrackingFollowUpDate, staged.SourceTrackingFollowUpDate),
    target_case.MinorComp = COALESCE(target_case.MinorComp, staged.SourceMinorComp),
    target_case.CaseDropped = COALESCE(target_case.CaseDropped, staged.SourceCaseDropped),
    target_case.ImportedCreatedByName = COALESCE(target_case.ImportedCreatedByName, staged.SourceImportedCreatedByName),
    target_case.UpdatedAtUtc = UTC_TIMESTAMP(6),
    target_case.UpdatedByUserId = @migration_user_id
WHERE @apply_permitted = 1
  AND staged.BlockingReason IS NULL
  AND (staged.NeedsLawFirm = 1
    OR staged.NeedsCaseManager = 1
    OR staged.NeedsAttorney = 1
    OR staged.NeedsAccidentTypeId = 1
    OR staged.NeedsAccidentTypeName = 1
    OR staged.NeedsStatusLabel = 1
    OR staged.NeedsParityFields = 1)
  AND target_case.TenantId = @tenant_id
  AND target_case.OrgId = @org_id
  AND target_case.Notes <=> staged.NotesBefore
  AND target_case.ClientDob <=> staged.ExistingClientDob
  AND target_case.ClientAddressLine1 <=> staged.ExistingClientAddressLine1
  AND target_case.ClientCity <=> staged.ExistingClientCity
  AND target_case.ClientState <=> staged.ExistingClientState
  AND target_case.ClientPostalCode <=> staged.ExistingClientPostalCode
  AND target_case.ClientPhone <=> staged.ExistingClientPhone
  AND target_case.ClientEmail <=> staged.ExistingClientEmail
  AND target_case.IncidentState <=> staged.ExistingIncidentState
  AND target_case.CurrentMedicalStatus <=> staged.ExistingMedicalStatus
  AND target_case.TrackingFollowUpDate <=> staged.ExistingTrackingFollowUpDate
  AND target_case.MinorComp <=> staged.ExistingMinorComp
  AND target_case.CaseDropped <=> staged.ExistingCaseDropped
  AND target_case.ImportedCreatedByName <=> staged.ExistingImportedCreatedByName;
SET @case_rows_updated = ROW_COUNT();

INSERT INTO liens_LegacyFieldMigrationStates (
    Id, TenantId, SourceSystem, SourceTable, LegacyId, MappingVersion,
    FieldGroup, TargetEntity, TargetId, SourceHash, TargetPreimageHash,
    AppliedValueHash, Status, ImportRunId, AppliedAtUtc, CreatedAtUtc)
SELECT
    UUID(), @tenant_id, 'SL-CORE', 'SL_CASE', CAST(staged.LegacyCaseId AS CHAR),
    'sl-core-report-parity-v2', 'CaseReportParity', 'Case', staged.TargetCaseId,
    staged.ParitySourceHash,
    SHA2(CAST(JSON_ARRAY(staged.ExistingClientDob,
        staged.ExistingClientAddressLine1, staged.ExistingClientCity,
        staged.ExistingClientState, staged.ExistingClientPostalCode,
        staged.ExistingClientPhone, staged.ExistingClientEmail,
        staged.ExistingIncidentState, staged.ExistingMedicalStatus,
        staged.ExistingTrackingFollowUpDate, staged.ExistingMinorComp,
        staged.ExistingCaseDropped, staged.ExistingImportedCreatedByName) AS CHAR), 256),
    SHA2(CAST(JSON_ARRAY(staged.SourceClientDob,
        staged.SourceClientAddressLine1, staged.SourceClientCity,
        staged.SourceClientState, staged.SourceClientPostalCode,
        staged.SourceClientPhone, staged.SourceClientEmail,
        staged.SourceIncidentState, staged.SourceMedicalStatus,
        staged.SourceTrackingFollowUpDate, staged.SourceMinorComp,
        staged.SourceCaseDropped, staged.SourceImportedCreatedByName) AS CHAR), 256),
    'Applied', @core_run_id, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM tmp_sl_core_case_relationships staged
WHERE @apply_permitted = 1
  AND staged.BlockingReason IS NULL
  AND staged.NeedsParityFields = 1
  AND NOT EXISTS (
      SELECT 1
      FROM liens_LegacyFieldMigrationStates existing
      WHERE existing.TenantId = @tenant_id
        AND existing.SourceSystem = 'SL-CORE'
        AND existing.SourceTable = 'SL_CASE'
        AND BINARY existing.LegacyId = BINARY CAST(staged.LegacyCaseId AS CHAR)
        AND existing.MappingVersion = 'sl-core-report-parity-v2'
        AND existing.FieldGroup = 'CaseReportParity'
  );
SET @parity_ledger_rows_inserted = ROW_COUNT();

UPDATE liens_Liens target_lien
INNER JOIN tmp_sl_core_lien_facility_updates staged
  ON staged.TargetLienId = target_lien.Id
SET target_lien.FacilityId = staged.TargetFacilityId,
    target_lien.UpdatedAtUtc = UTC_TIMESTAMP(6),
    target_lien.UpdatedByUserId = @migration_user_id
WHERE @apply_permitted = 1
  AND target_lien.TenantId = @tenant_id
  AND target_lien.OrgId = @org_id
  AND target_lien.FacilityId IS NULL;
SET @facility_rows_updated = ROW_COUNT();

INSERT INTO liens_ServicingItems
  (Id, TenantId, OrgId, TaskNumber, TaskType, Description, Status,
   Priority, AssignedTo, AssignedToUserId, CaseId, LienId, DueDate,
   Notes, Resolution, StartedAtUtc, CompletedAtUtc, EscalatedAtUtc,
   CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
SELECT
  UUID(), @tenant_id, @org_id, staged.TaskNumber, 'LegacyMedicalCode',
  CASE WHEN staged.SafeCode IS NULL THEN 'Legacy medical code entry'
       ELSE CONCAT('Medical code ', staged.SafeCode) END,
  'Pending', 'Normal', 'system', NULL, staged.TargetCaseId, staged.TargetLienId,
  NULL, staged.ExpectedNotes, NULL, NULL, NULL, NULL,
  @migration_user_id, @migration_user_id, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM tmp_sl_core_report_medical_codes staged
LEFT JOIN liens_ServicingItems existing_task
  ON existing_task.TenantId = @tenant_id
 AND BINARY existing_task.TaskNumber = BINARY staged.TaskNumber
WHERE @apply_permitted = 1
  AND staged.Resolution = 'NeedsInsert'
  AND existing_task.Id IS NULL;
SET @medical_code_rows_inserted = ROW_COUNT();

INSERT INTO liens_ServicingItems
  (Id, TenantId, OrgId, TaskNumber, TaskType, Description, Status,
   Priority, AssignedTo, AssignedToUserId, CaseId, LienId, DueDate,
   Notes, Resolution, StartedAtUtc, CompletedAtUtc, EscalatedAtUtc,
   CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
SELECT
  UUID(), @tenant_id, @org_id, staged.TaskNumber, 'LegacyMedicalFacilityInfo',
  'Legacy medical facility information', 'Pending', 'Normal', 'system', NULL,
  staged.TargetCaseId, staged.TargetLienId, NULL, staged.NotesAfter,
  NULL, NULL, NULL, NULL,
  @migration_user_id, @migration_user_id, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM tmp_sl_core_report_providers staged
LEFT JOIN liens_ServicingItems existing_task
  ON existing_task.TenantId = @tenant_id
 AND BINARY existing_task.TaskNumber = BINARY staged.TaskNumber
WHERE @apply_permitted = 1
  AND staged.Resolution = 'NeedsInsert'
  AND existing_task.Id IS NULL;
SET @provider_rows_inserted = ROW_COUNT();

UPDATE liens_ServicingItems target_task
INNER JOIN tmp_sl_core_report_providers staged
  ON staged.ExistingTaskId = target_task.Id
SET target_task.Notes = staged.NotesAfter,
    target_task.UpdatedAtUtc = UTC_TIMESTAMP(6),
    target_task.UpdatedByUserId = @migration_user_id
WHERE @apply_permitted = 1
  AND staged.Resolution = 'NeedsUpdate'
  AND target_task.TenantId = @tenant_id
  AND target_task.OrgId = @org_id
  AND target_task.Notes <=> staged.ExistingNotes;
SET @provider_rows_updated = ROW_COUNT();

INSERT INTO liens_CaseNotes
  (Id, CaseId, TenantId, Content, Category, IsPinned,
   CreatedByUserId, CreatedByName, IsEdited, IsDeleted,
   CreatedAtUtc, UpdatedAtUtc)
SELECT
  staged.TargetActivityId, staged.TargetCaseId, @tenant_id,
  staged.ActivityContent, 'internal', 0,
  @migration_user_id, staged.ActivityCreatedByName, 0, 0,
  staged.ActivityAtUtc, NULL
FROM tmp_sl_core_report_activities staged
LEFT JOIN liens_LegacyIdCrosswalks existing_x
  ON existing_x.TenantId = @tenant_id
 AND existing_x.SourceSystem = 'SL-CORE'
 AND existing_x.SourceTable = 'SL_CASE_NOTES_LAST_ACTIVITY'
 AND BINARY existing_x.LegacyId = BINARY CAST(staged.LegacyCaseId AS CHAR)
 AND existing_x.TargetEntity = 'CaseNote'
WHERE @apply_permitted = 1
  AND staged.Resolution = 'NeedsInsert'
  AND existing_x.Id IS NULL;
SET @activity_rows_inserted = ROW_COUNT();

INSERT INTO liens_LegacyIdCrosswalks
  (Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
   TargetId, SourceHash, ImportRunId, CreatedAtUtc)
SELECT
  UUID(), @tenant_id, 'SL-CORE', 'SL_CASE_NOTES_LAST_ACTIVITY',
  CAST(staged.LegacyCaseId AS CHAR), 'CaseNote', staged.TargetActivityId,
  staged.SourceHash, @core_run_id, UTC_TIMESTAMP(6)
FROM tmp_sl_core_report_activities staged
LEFT JOIN liens_LegacyIdCrosswalks existing_x
  ON existing_x.TenantId = @tenant_id
 AND existing_x.SourceSystem = 'SL-CORE'
 AND existing_x.SourceTable = 'SL_CASE_NOTES_LAST_ACTIVITY'
 AND BINARY existing_x.LegacyId = BINARY CAST(staged.LegacyCaseId AS CHAR)
 AND existing_x.TargetEntity = 'CaseNote'
WHERE @apply_permitted = 1
  AND staged.Resolution = 'NeedsInsert'
  AND existing_x.Id IS NULL;
SET @activity_crosswalks_inserted = ROW_COUNT();

SELECT COUNT(*) INTO @case_postcondition_errors
FROM tmp_sl_core_case_relationships staged
INNER JOIN liens_Cases target_case
  ON target_case.Id = staged.TargetCaseId
WHERE @apply_permitted = 1
  AND staged.BlockingReason IS NULL
  AND (staged.NeedsLawFirm = 1
    OR staged.NeedsCaseManager = 1
    OR staged.NeedsAttorney = 1
    OR staged.NeedsAccidentTypeId = 1
    OR staged.NeedsAccidentTypeName = 1
    OR staged.NeedsStatusLabel = 1
    OR staged.NeedsParityFields = 1)
  AND (
      (staged.NeedsLawFirm = 1
       AND LOCATE(CONCAT('lawFirmId=', staged.TargetLawFirmId), target_case.Notes) = 0)
   OR (staged.NeedsCaseManager = 1
       AND LOCATE(CONCAT('caseManagerId=', staged.TargetCaseManagerId), target_case.Notes) = 0)
   OR (staged.NeedsAttorney = 1
       AND LOCATE(CONCAT('attorneyId=', staged.TargetAttorneyId), target_case.Notes) = 0)
   OR (staged.NeedsAccidentTypeId = 1
       AND LOCATE(CONCAT('accidentTypeId=', staged.TargetAccidentTypeId), target_case.Notes) = 0)
   OR (staged.NeedsAccidentTypeName = 1
       AND LOCATE(CONCAT('accidentType=', staged.TargetAccidentTypeName), target_case.Notes) = 0)
   OR (staged.NeedsStatusLabel = 1
       AND LOCATE(CONCAT('statusLabel=', staged.SourceStatusLabel), target_case.Notes) = 0)
   OR (staged.SourceClientDob IS NOT NULL AND NOT (target_case.ClientDob <=> staged.SourceClientDob))
   OR (staged.SourceClientAddressLine1 IS NOT NULL AND NOT (target_case.ClientAddressLine1 <=> staged.SourceClientAddressLine1))
   OR (staged.SourceClientCity IS NOT NULL AND NOT (target_case.ClientCity <=> staged.SourceClientCity))
   OR (staged.SourceClientState IS NOT NULL AND NOT (target_case.ClientState <=> staged.SourceClientState))
   OR (staged.SourceClientPostalCode IS NOT NULL AND NOT (target_case.ClientPostalCode <=> staged.SourceClientPostalCode))
   OR (staged.SourceClientPhone IS NOT NULL AND NOT (target_case.ClientPhone <=> staged.SourceClientPhone))
   OR (staged.SourceClientEmail IS NOT NULL AND NOT (target_case.ClientEmail <=> staged.SourceClientEmail))
   OR (staged.SourceIncidentState IS NOT NULL AND NOT (target_case.IncidentState <=> staged.SourceIncidentState))
   OR (staged.SourceMedicalStatus IS NOT NULL AND NOT (target_case.CurrentMedicalStatus <=> staged.SourceMedicalStatus))
   OR (staged.SourceTrackingFollowUpDate IS NOT NULL AND NOT (target_case.TrackingFollowUpDate <=> staged.SourceTrackingFollowUpDate))
   OR (staged.SourceMinorComp IS NOT NULL AND NOT (target_case.MinorComp <=> staged.SourceMinorComp))
   OR (staged.SourceCaseDropped IS NOT NULL AND NOT (target_case.CaseDropped <=> staged.SourceCaseDropped))
   OR (staged.SourceImportedCreatedByName IS NOT NULL AND NOT (target_case.ImportedCreatedByName <=> staged.SourceImportedCreatedByName))
   OR (staged.NeedsParityFields = 1 AND NOT EXISTS (
        SELECT 1 FROM liens_LegacyFieldMigrationStates ledger
        WHERE ledger.TenantId = @tenant_id
          AND ledger.SourceSystem = 'SL-CORE'
          AND ledger.SourceTable = 'SL_CASE'
          AND BINARY ledger.LegacyId = BINARY CAST(staged.LegacyCaseId AS CHAR)
          AND ledger.MappingVersion = 'sl-core-report-parity-v2'
          AND ledger.FieldGroup = 'CaseReportParity'
          AND ledger.TargetEntity = 'Case'
          AND ledger.TargetId = staged.TargetCaseId
          AND BINARY ledger.SourceHash = BINARY staged.ParitySourceHash
          AND ledger.ImportRunId = @core_run_id
          AND ledger.Status = 'Applied'))
  );

SELECT COUNT(*) INTO @facility_postcondition_errors
FROM tmp_sl_core_lien_facility_updates staged
INNER JOIN liens_Liens target_lien
  ON target_lien.Id = staged.TargetLienId
WHERE @apply_permitted = 1
  AND target_lien.FacilityId <> staged.TargetFacilityId;

SELECT COUNT(*) INTO @medical_code_postcondition_errors
FROM tmp_sl_core_report_medical_codes staged
LEFT JOIN liens_ServicingItems target_task
  ON target_task.TenantId = @tenant_id
 AND BINARY target_task.TaskNumber = BINARY staged.TaskNumber
WHERE @apply_permitted = 1
  AND (target_task.Id IS NULL
    OR target_task.TaskType <> 'LegacyMedicalCode'
    OR NOT (target_task.CaseId <=> staged.TargetCaseId)
    OR NOT (target_task.LienId <=> staged.TargetLienId)
    OR BINARY COALESCE(target_task.Notes, '') <> BINARY staged.ExpectedNotes);

SELECT COUNT(*) INTO @provider_postcondition_errors
FROM tmp_sl_core_report_providers staged
LEFT JOIN liens_ServicingItems target_task
  ON target_task.TenantId = @tenant_id
 AND target_task.LienId = staged.TargetLienId
 AND target_task.TaskType = 'LegacyMedicalFacilityInfo'
WHERE @apply_permitted = 1
  AND (target_task.Id IS NULL
    OR LOCATE(CONCAT('medicalProviderId=', staged.TargetProviderId), target_task.Notes) = 0);

SELECT COUNT(*) INTO @activity_postcondition_errors
FROM tmp_sl_core_report_activities staged
LEFT JOIN liens_LegacyIdCrosswalks activity_x
  ON activity_x.TenantId = @tenant_id
 AND activity_x.SourceSystem = 'SL-CORE'
 AND activity_x.SourceTable = 'SL_CASE_NOTES_LAST_ACTIVITY'
 AND BINARY activity_x.LegacyId = BINARY CAST(staged.LegacyCaseId AS CHAR)
 AND activity_x.TargetEntity = 'CaseNote'
LEFT JOIN liens_CaseNotes target_activity ON target_activity.Id = activity_x.TargetId
WHERE @apply_permitted = 1
  AND (activity_x.Id IS NULL
    OR activity_x.TargetId <> staged.TargetActivityId
    OR BINARY activity_x.SourceHash <> BINARY staged.SourceHash
    OR activity_x.ImportRunId <> @core_run_id
    OR target_activity.Id IS NULL
    OR target_activity.TenantId <> @tenant_id
    OR target_activity.CaseId <> staged.TargetCaseId
    OR BINARY target_activity.Content <> BINARY staged.ActivityContent
    OR target_activity.Category <> 'internal'
    OR target_activity.IsDeleted <> 0
    OR target_activity.CreatedAtUtc <> staged.ActivityAtUtc
    OR target_activity.UpdatedAtUtc IS NOT NULL);

SET @apply_succeeded =
    @apply_permitted = 1
    AND @case_rows_updated = @case_update_count
    AND @parity_ledger_rows_inserted = @parity_field_updates
    AND @facility_rows_updated = @facility_update_count
    AND @medical_code_rows_inserted = @medical_code_insert_count
    AND @provider_rows_inserted = @provider_insert_count
    AND @provider_rows_updated = @provider_update_count
    AND @activity_rows_inserted = @activity_insert_count
    AND @activity_crosswalks_inserted = @activity_insert_count
    AND @case_postcondition_errors = 0
    AND @facility_postcondition_errors = 0
    AND @medical_code_postcondition_errors = 0
    AND @provider_postcondition_errors = 0
    AND @activity_postcondition_errors = 0;

-- Deliberately no COMMIT or ROLLBACK follows. The .NET runner reads
-- @apply_succeeded and then commits or rolls back in a finally-safe boundary.
-- Do not set @apply = 1 and execute this SQL file directly in DBeaver.

SELECT
    @apply_permitted AS ApplyPermittedAfterRowLocks,
    @apply_succeeded AS ApplyCommitted,
    @case_preimage_matches AS CasePreimagesMatched,
    @facility_preimage_matches AS FacilityPreimagesMatched,
    @case_rows_updated AS CaseRowsUpdated,
    @parity_ledger_rows_inserted AS ParityLedgerRowsInserted,
    @facility_rows_updated AS LienFacilityRowsUpdated,
    @medical_code_rows_inserted AS MedicalCodeRowsInserted,
    @provider_rows_inserted AS MedicalProviderRowsInserted,
    @provider_rows_updated AS MedicalProviderRowsUpdated,
    @activity_rows_inserted AS LastActivityRowsInserted,
    @case_postcondition_errors AS CasePostconditionErrors,
    @facility_postcondition_errors AS FacilityPostconditionErrors,
    @medical_code_postcondition_errors AS MedicalCodePostconditionErrors,
    @provider_postcondition_errors AS MedicalProviderPostconditionErrors,
    @activity_postcondition_errors AS LastActivityPostconditionErrors;
