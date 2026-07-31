-- SL-CORE Program 1 relationship backfill (DBeaver/MySQL script)
--
-- Restores only relationships omitted by the historical core importer:
--   * SL_CASE.CASE_MANAGER -> case Notes metadata caseManagerId
--   * SL_CASE.CASE_ACCIDENT_TYPE -> case Notes metadata accidentTypeId/type
--   * SL_LEINS_MEDICAL_INFORMATION_FACILITY -> liens_Liens.FacilityId
--
-- This is intentionally an ordinary SQL script: it has no stored procedure and
-- no DELIMITER command. Run the complete file in DBeaver only with @apply = 0.
-- The checked-in .NET runner executes an apply and owns COMMIT/ROLLBACK so an
-- error, cancellation, or failed postcondition cannot leave a partial repair.
--
-- The script does not create contacts, facilities, lookup values, or crosswalks.
-- It requires the approved Program 1 core and contacts/facilities imports to
-- have completed first.  Missing/ambiguous mappings or existing conflicting
-- assignments block every write.

SET @tenant_id = '019f6ae6-4348-784a-aae0-f4d636f843ad';
SET @legacy_program = '1';

-- Dry-run settings.  Leave these unchanged for the first execution.
SET @apply = 0;
SET @expected_case_updates = -1;
SET @expected_lien_facility_updates = -1;

-- For an apply, run the dry-run first.  Copy its exact CaseRowsToUpdate and
-- LienFacilityRowsToUpdate values here, then set @apply = 1 and rerun all.

SET @target_schema = DATABASE();
SET @tenant_id = LOWER(TRIM(@tenant_id));
SET @apply = IF(@apply = 1, 1, 0);

SELECT COUNT(*) INTO @target_table_count
FROM information_schema.tables
WHERE table_schema = @target_schema
  AND table_type = 'BASE TABLE'
  AND table_name IN (
      'liens_Cases', 'liens_Contacts', 'liens_Facilities', 'liens_Liens',
      'liens_LookupValues', 'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns'
  );

SELECT COUNT(*) INTO @source_table_count
FROM information_schema.tables
WHERE table_schema = 'SL-CORE'
  AND table_type = 'BASE TABLE'
  AND table_name IN (
      'SL_CASE', 'SL_CONTACT', 'SL_ACCIDENT_TYPE',
      'SL_LEINS_MEDICAL_INFORMATION_FACILITY',
      'SL_MIGRATION_SOURCE_PROVENANCE'
  );

SELECT COUNT(*), MAX(r.Id), MAX(r.OrgId), MAX(r.CreatedByUserId),
       MAX(LOWER(r.SourceFingerprint))
  INTO @core_run_count, @core_run_id, @org_id, @migration_user_id,
       @source_fingerprint
FROM liens_LegacyImportRuns r
WHERE r.TenantId = @tenant_id
  AND r.SourceSystem = 'SL-CORE'
  AND r.LegacyProgram = @legacy_program
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
  AND r.LegacyProgram = @legacy_program
  AND r.MappingVersion = 'sl-core-contact-facility-v1'
  AND LOWER(r.SourceFingerprint) = @source_fingerprint
  AND r.Status = 'Completed'
  AND EXISTS (
      SELECT 1
      FROM liens_LegacyIdCrosswalks x
      WHERE x.TenantId = r.TenantId
        AND x.ImportRunId = r.Id
        AND x.SourceSystem = 'SL-CORE'
        AND x.SourceTable = 'SL_CONTACT'
        AND x.TargetEntity = 'Contact'
  );

SELECT COUNT(*) INTO @provenance_count
FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE` p
WHERE p.PROVENANCE_KEY = 'sl-core-current'
  AND LOWER(p.SOURCE_FINGERPRINT) = @source_fingerprint
  AND p.IMPORT_SCOPE = 'sl-core-core-liens-v1';

SET @preflight_ok =
    @target_schema IN ('LS_QA_LIENS', 'LS_LIENS')
    AND @tenant_id REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
    AND @target_table_count = 7
    AND @source_table_count = 5
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
    target_case.Notes AS NotesBefore,
    CASE
        WHEN source_case.CASE_MANAGER IS NULL OR source_case.CASE_MANAGER = 0 THEN 0
        ELSE 1
    END AS SourceCaseManagerRequired,
    source_case.CASE_MANAGER AS LegacyCaseManagerId,
    source_manager.CONTACT_ID AS SourceCaseManagerId,
    manager_x.Id AS ManagerCrosswalkId,
    manager_x.TargetId AS TargetCaseManagerId,
    target_manager.TenantId AS TargetManagerTenantId,
    target_manager.OrgId AS TargetManagerOrgId,
    target_manager.ContactType AS TargetManagerContactType,
    target_manager.ContactSubtype AS TargetManagerContactSubtype,
    target_manager.IsActive AS TargetManagerIsActive,
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
 AND case_x.LegacyId = CAST(source_case.CASE_ID AS CHAR)
 AND case_x.TargetEntity = 'Case'
 AND case_x.ImportRunId = @core_run_id
LEFT JOIN liens_Cases target_case
  ON target_case.Id = case_x.TargetId
LEFT JOIN `SL-CORE`.`SL_CONTACT` source_manager
  ON source_manager.CONTACT_ID = source_case.CASE_MANAGER
 AND source_manager.CONTACT_PROGRAM = @legacy_program
 AND source_manager.CONTACT_TYPE = 6
 AND COALESCE(source_manager.CONTACT_STATUS, 'A') = 'A'
LEFT JOIN liens_LegacyIdCrosswalks manager_x
  ON manager_x.TenantId = @tenant_id
 AND manager_x.SourceSystem = 'SL-CORE'
 AND manager_x.SourceTable = 'SL_CONTACT'
 AND manager_x.LegacyId = CAST(source_case.CASE_MANAGER AS CHAR)
 AND manager_x.TargetEntity = 'Contact'
 AND manager_x.ImportRunId = @contact_run_id
LEFT JOIN liens_Contacts target_manager
  ON target_manager.Id = manager_x.TargetId
LEFT JOIN `SL-CORE`.`SL_ACCIDENT_TYPE` source_accident
  ON source_accident.AT_ID = source_case.CASE_ACCIDENT_TYPE
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
  ON accident_lookup.NormalizedName = LOWER(TRIM(source_accident.AT_DESCRIPTION))
WHERE source_case.CASE_PROGRAM = @legacy_program
  AND COALESCE(source_case.CASE_IS_DELETED, 'N') <> 'Y';

ALTER TABLE tmp_sl_core_case_relationships
    ADD COLUMN MetadataText VARCHAR(4000) NULL,
    ADD COLUMN ExistingCaseManagerId VARCHAR(100) NULL,
    ADD COLUMN ExistingCaseManagerCount INT NOT NULL DEFAULT 0,
    ADD COLUMN ExistingAccidentTypeId VARCHAR(100) NULL,
    ADD COLUMN ExistingAccidentTypeIdCount INT NOT NULL DEFAULT 0,
    ADD COLUMN ExistingAccidentTypeName VARCHAR(250) NULL,
    ADD COLUMN ExistingAccidentTypeNameCount INT NOT NULL DEFAULT 0,
    ADD COLUMN BlockingReason VARCHAR(300) NULL,
    ADD COLUMN NeedsCaseManager TINYINT NOT NULL DEFAULT 0,
    ADD COLUMN NeedsAccidentTypeId TINYINT NOT NULL DEFAULT 0,
    ADD COLUMN NeedsAccidentTypeName TINYINT NOT NULL DEFAULT 0,
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
SET ExistingCaseManagerCount = CASE
        WHEN MetadataText IS NULL THEN 0
        ELSE (CHAR_LENGTH(LOWER(MetadataText))
              - CHAR_LENGTH(REPLACE(LOWER(MetadataText), 'casemanagerid=', '')))
             / CHAR_LENGTH('caseManagerId=')
    END,
    ExistingCaseManagerId = NULLIF(TRIM(SUBSTRING_INDEX(
        REGEXP_SUBSTR(COALESCE(MetadataText, ''),
            '(^|;[[:space:]]*)caseManagerId=[^;[:space:]]*', 1, 1, 'i'),
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
        '=', -1)), '');

UPDATE tmp_sl_core_case_relationships
SET BlockingReason = CASE
    WHEN CaseCrosswalkId IS NULL THEN 'MissingCaseCrosswalk'
    WHEN TargetCaseId IS NULL
      OR TargetCaseTenantId <> @tenant_id
      OR TargetCaseOrgId <> @org_id THEN 'InvalidTargetCase'
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
    WHEN SourceAccidentTypeRequired = 1 AND LegacyAccidentTypeName IS NULL
      THEN 'MissingLegacyAccidentType'
    WHEN SourceAccidentTypeRequired = 1 AND TargetAccidentTypeMatchCount <> 1
      THEN 'MissingOrAmbiguousTargetAccidentTypeLookup'
    WHEN MetadataMarkerCount > 1 THEN 'AmbiguousLegacyMetadata'
    WHEN MetadataMarkerCount = 0
      AND TRIM(COALESCE(NotesBefore, '')) REGEXP
          '^[A-Za-z0-9_]+=[^;]+(;[[:space:]]*[A-Za-z0-9_]+=[^;]+)*$'
      AND REGEXP_LIKE(NotesBefore,
          '(^|;[[:space:]]*)(caseManagerId|accidentTypeId|accidentType)=', 'i')
      THEN 'UnmarkedLegacyMetadata'
    WHEN ExistingCaseManagerCount > 1 THEN 'DuplicateCaseManagerMetadata'
    WHEN ExistingAccidentTypeIdCount > 1 THEN 'DuplicateAccidentTypeIdMetadata'
    WHEN ExistingAccidentTypeNameCount > 1 THEN 'DuplicateAccidentTypeMetadata'
    WHEN SourceCaseManagerRequired = 1
      AND ExistingCaseManagerCount = 1
      AND ExistingCaseManagerId IS NULL THEN 'MalformedExistingCaseManagerId'
    WHEN SourceCaseManagerRequired = 1
      AND ExistingCaseManagerId IS NOT NULL
      AND LOWER(ExistingCaseManagerId) <> LOWER(TargetCaseManagerId)
      THEN 'ConflictingExistingCaseManagerId'
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
    ELSE NULL
END;

UPDATE tmp_sl_core_case_relationships
SET NeedsCaseManager = CASE
        WHEN BlockingReason IS NULL
         AND SourceCaseManagerRequired = 1
         AND ExistingCaseManagerCount = 0 THEN 1 ELSE 0 END,
    NeedsAccidentTypeId = CASE
        WHEN BlockingReason IS NULL
         AND SourceAccidentTypeRequired = 1
         AND ExistingAccidentTypeIdCount = 0 THEN 1 ELSE 0 END,
    NeedsAccidentTypeName = CASE
        WHEN BlockingReason IS NULL
         AND SourceAccidentTypeRequired = 1
         AND ExistingAccidentTypeNameCount = 0 THEN 1 ELSE 0 END;

UPDATE tmp_sl_core_case_relationships
SET NotesAfter = CASE
    WHEN NeedsCaseManager = 0
     AND NeedsAccidentTypeId = 0
     AND NeedsAccidentTypeName = 0 THEN NotesBefore
    WHEN NotesBefore IS NULL OR TRIM(NotesBefore) = '' THEN CONCAT(
        '[legacy-meta]', CHAR(10),
        CONCAT_WS('; ',
            CASE WHEN NeedsCaseManager = 1
                 THEN CONCAT('caseManagerId=', TargetCaseManagerId) END,
            CASE WHEN NeedsAccidentTypeId = 1
                 THEN CONCAT('accidentTypeId=', TargetAccidentTypeId) END,
            CASE WHEN NeedsAccidentTypeName = 1
                 THEN CONCAT('accidentType=', TargetAccidentTypeName) END
        )
    )
    WHEN MetadataMarkerCount = 0 THEN CONCAT(
        NotesBefore, CHAR(10), CHAR(10), '[legacy-meta]', CHAR(10),
        CONCAT_WS('; ',
            CASE WHEN NeedsCaseManager = 1
                 THEN CONCAT('caseManagerId=', TargetCaseManagerId) END,
            CASE WHEN NeedsAccidentTypeId = 1
                 THEN CONCAT('accidentTypeId=', TargetAccidentTypeId) END,
            CASE WHEN NeedsAccidentTypeName = 1
                 THEN CONCAT('accidentType=', TargetAccidentTypeName) END
        )
    )
    WHEN MetadataText IS NULL OR MetadataText = '' THEN CONCAT(
        NotesBefore, CHAR(10),
        CONCAT_WS('; ',
            CASE WHEN NeedsCaseManager = 1
                 THEN CONCAT('caseManagerId=', TargetCaseManagerId) END,
            CASE WHEN NeedsAccidentTypeId = 1
                 THEN CONCAT('accidentTypeId=', TargetAccidentTypeId) END,
            CASE WHEN NeedsAccidentTypeName = 1
                 THEN CONCAT('accidentType=', TargetAccidentTypeName) END
        )
    )
    ELSE CONCAT(
        NotesBefore, '; ',
        CONCAT_WS('; ',
            CASE WHEN NeedsCaseManager = 1
                 THEN CONCAT('caseManagerId=', TargetCaseManagerId) END,
            CASE WHEN NeedsAccidentTypeId = 1
                 THEN CONCAT('accidentTypeId=', TargetAccidentTypeId) END,
            CASE WHEN NeedsAccidentTypeName = 1
                 THEN CONCAT('accidentType=', TargetAccidentTypeName) END
        )
    )
END;

UPDATE tmp_sl_core_case_relationships
SET BlockingReason = 'NotesOverflow',
    NeedsCaseManager = 0,
    NeedsAccidentTypeId = 0,
    NeedsAccidentTypeName = 0
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
 AND lien_x.LegacyId = CAST(source_link.LMI_LM_ID AS CHAR)
 AND lien_x.TargetEntity = 'Lien'
 AND lien_x.ImportRunId = @core_run_id
LEFT JOIN liens_Liens target_lien
  ON target_lien.Id = lien_x.TargetId
LEFT JOIN liens_LegacyIdCrosswalks facility_x
  ON facility_x.TenantId = @tenant_id
 AND facility_x.SourceSystem = 'SL-CORE'
 AND facility_x.SourceTable = 'SL_FACILITY'
 AND facility_x.LegacyId = CAST(source_link.LMI_FACILITY_ID AS CHAR)
 AND facility_x.TargetEntity = 'Facility'
 AND facility_x.ImportRunId = @contact_run_id
LEFT JOIN liens_Facilities target_facility
  ON target_facility.Id = facility_x.TargetId
LEFT JOIN liens_LegacyIdCrosswalks link_x
  ON link_x.TenantId = @tenant_id
 AND link_x.SourceSystem = 'SL-CORE'
 AND link_x.SourceTable = 'SL_LEINS_MEDICAL_INFORMATION_FACILITY'
 AND link_x.LegacyId = CAST(source_link.LMI_ID AS CHAR)
 AND link_x.TargetEntity = 'LienFacilityLink'
 AND link_x.ImportRunId = @contact_run_id
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

SELECT COUNT(*) INTO @case_source_count
FROM tmp_sl_core_case_relationships;
SELECT COUNT(*) INTO @case_conflict_count
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NOT NULL;
SELECT COUNT(*) INTO @case_update_count
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NULL
  AND (NeedsCaseManager = 1
    OR NeedsAccidentTypeId = 1
    OR NeedsAccidentTypeName = 1);
SELECT COUNT(*) INTO @case_manager_updates
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NULL AND NeedsCaseManager = 1;
SELECT COUNT(*) INTO @accident_type_updates
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NULL AND NeedsAccidentTypeId = 1;
SELECT COUNT(*) INTO @facility_conflict_count
FROM tmp_sl_core_lien_facility_relationships
WHERE BlockingReason IS NOT NULL;
SELECT COUNT(*) INTO @facility_update_count
FROM tmp_sl_core_lien_facility_updates;

SET @apply_permitted =
    @apply = 1
    AND @preflight_ok = 1
    AND @case_conflict_count = 0
    AND @facility_conflict_count = 0
    AND @expected_case_updates = @case_update_count
    AND @expected_lien_facility_updates = @facility_update_count;

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
    @case_manager_updates AS CaseManagerRelationsToAdd,
    @accident_type_updates AS AccidentTypeRelationsToAdd,
    @facility_update_count AS LienFacilityRowsToUpdate,
    @case_conflict_count AS CaseConflicts,
    @facility_conflict_count AS FacilityConflicts;

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

SELECT LegacyCaseId, TargetCaseId, LegacyCaseManagerId, LegacyAccidentTypeId,
       LegacyAccidentTypeName, TargetCaseManagerId, TargetAccidentTypeId,
       BlockingReason
FROM tmp_sl_core_case_relationships
WHERE BlockingReason IS NOT NULL
ORDER BY LegacyCaseId
LIMIT 100;

-- The guarded transaction changes nothing unless every preflight condition is
-- valid, there are zero conflicts, @apply is 1, and both expected counts match.
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
START TRANSACTION;

SELECT COUNT(*) INTO @case_preimage_matches
FROM liens_Cases target_case
INNER JOIN tmp_sl_core_case_relationships staged
  ON staged.TargetCaseId = target_case.Id
WHERE @apply_permitted = 1
  AND staged.BlockingReason IS NULL
  AND (staged.NeedsCaseManager = 1
    OR staged.NeedsAccidentTypeId = 1
    OR staged.NeedsAccidentTypeName = 1)
  AND target_case.TenantId = @tenant_id
  AND target_case.OrgId = @org_id
  AND target_case.Notes <=> staged.NotesBefore
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

SET @apply_permitted =
    @apply_permitted = 1
    AND @case_preimage_matches = @case_update_count
    AND @facility_preimage_matches = @facility_update_count;

UPDATE liens_Cases target_case
INNER JOIN tmp_sl_core_case_relationships staged
  ON staged.TargetCaseId = target_case.Id
SET target_case.Notes = staged.NotesAfter,
    target_case.UpdatedAtUtc = UTC_TIMESTAMP(6),
    target_case.UpdatedByUserId = @migration_user_id
WHERE @apply_permitted = 1
  AND staged.BlockingReason IS NULL
  AND (staged.NeedsCaseManager = 1
    OR staged.NeedsAccidentTypeId = 1
    OR staged.NeedsAccidentTypeName = 1)
  AND target_case.TenantId = @tenant_id
  AND target_case.OrgId = @org_id
  AND target_case.Notes <=> staged.NotesBefore;
SET @case_rows_updated = ROW_COUNT();

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

SELECT COUNT(*) INTO @case_postcondition_errors
FROM tmp_sl_core_case_relationships staged
INNER JOIN liens_Cases target_case
  ON target_case.Id = staged.TargetCaseId
WHERE @apply_permitted = 1
  AND staged.BlockingReason IS NULL
  AND (staged.NeedsCaseManager = 1
    OR staged.NeedsAccidentTypeId = 1
    OR staged.NeedsAccidentTypeName = 1)
  AND (
      (staged.NeedsCaseManager = 1
       AND LOCATE(CONCAT('caseManagerId=', staged.TargetCaseManagerId), target_case.Notes) = 0)
   OR (staged.NeedsAccidentTypeId = 1
       AND LOCATE(CONCAT('accidentTypeId=', staged.TargetAccidentTypeId), target_case.Notes) = 0)
   OR (staged.NeedsAccidentTypeName = 1
       AND LOCATE(CONCAT('accidentType=', staged.TargetAccidentTypeName), target_case.Notes) = 0)
  );

SELECT COUNT(*) INTO @facility_postcondition_errors
FROM tmp_sl_core_lien_facility_updates staged
INNER JOIN liens_Liens target_lien
  ON target_lien.Id = staged.TargetLienId
WHERE @apply_permitted = 1
  AND target_lien.FacilityId <> staged.TargetFacilityId;

SET @apply_succeeded =
    @apply_permitted = 1
    AND @case_rows_updated = @case_update_count
    AND @facility_rows_updated = @facility_update_count
    AND @case_postcondition_errors = 0
    AND @facility_postcondition_errors = 0;

-- Deliberately no COMMIT or ROLLBACK follows. The .NET runner reads
-- @apply_succeeded and then commits or rolls back in a finally-safe boundary.
-- Do not set @apply = 1 and execute this SQL file directly in DBeaver.

SELECT
    @apply_permitted AS ApplyPermittedAfterRowLocks,
    @apply_succeeded AS ApplyCommitted,
    @case_preimage_matches AS CasePreimagesMatched,
    @facility_preimage_matches AS FacilityPreimagesMatched,
    @case_rows_updated AS CaseRowsUpdated,
    @facility_rows_updated AS LienFacilityRowsUpdated,
    @case_postcondition_errors AS CasePostconditionErrors,
    @facility_postcondition_errors AS FacilityPostconditionErrors;
