-- liens_backfill_sl_core_case_accident_types
--
-- Maps SL_CASE.CASE_ACCIDENT_TYPE to liens_Cases.Notes [legacy-meta] metadata.
-- Also seeds any AccidentType lookup values that are absent from the target
-- (Medical Malpractice, AT_ID=39, is the known missing entry in the initial
-- liens_LookupValues seed data).
--
-- Prerequisites: one completed Program 1 core import with case crosswalks.
-- Run this AFTER liens_import_sl_core_core_tenant_only has applied successfully.
-- The contacts/facilities import is NOT required by this procedure.
--
-- Metadata written into liens_Cases.Notes under the [legacy-meta] section:
--   accidentTypeId=<UUID from liens_LookupValues>; accidentType=<Name>
--
-- Preflight (p_apply = '0', p_expected_updates = -1):
--   Seeds missing AccidentType lookup values, then reports expected counts.
--   No permanent changes to liens_Cases.  Safe to run repeatedly.
--
-- Apply (p_apply = '1'):
--   p_expected_updates must equal the CasesNeedingUpdate from the preflight.
--   Seeds missing AccidentType lookup values, then updates liens_Cases.Notes
--   inside a SERIALIZABLE transaction with preimage and postcondition guards.
--
-- Usage:
--   CALL liens_backfill_sl_core_case_accident_types('<tenant-guid>', -1, '0');
--   CALL liens_backfill_sl_core_case_accident_types('<tenant-guid>', <N>, '1');
--
-- Resolution codes (informational, never block apply):
--   NoLegacyAccidentType       — CASE_ACCIDENT_TYPE IS NULL; case is skipped
--   AlreadyCorrect             — accidentTypeId is already present and matches
--
-- Resolution codes (conflicts; zero required before apply succeeds):
--   MissingCaseCrosswalk               — no crosswalk for this legacy case
--   InvalidTargetCase                  — target case not owned by tenant/org
--   MissingSourceAccidentType          — CASE_ACCIDENT_TYPE references a
--                                        non-existent SL_ACCIDENT_TYPE row
--   MissingTargetLookup                — no matching liens_LookupValues entry
--   AmbiguousTargetLookup              — multiple matching lookup entries
--   AmbiguousLegacyMetadata            — more than one [legacy-meta] marker
--   UnmarkedAccidentMetadata           — accidentTypeId= found outside marker
--   MalformedExistingAccidentTypeId    — marker present but UUID malformed
--   ConflictingExistingAccidentTypeId  — existing UUID differs from computed
--   NotesOverflow                      — update would exceed 4000-char limit


DROP PROCEDURE IF EXISTS liens_backfill_sl_core_case_accident_types;

DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_case_accident_types(
    IN p_tenant_id        CHAR(36),
    IN p_expected_updates INT,
    IN p_apply            CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id             CHAR(36);
    DECLARE v_apply                 BOOLEAN;
    DECLARE v_lock_name             VARCHAR(64);
    DECLARE v_lock_acquired         INT DEFAULT 0;
    DECLARE v_in_transaction        BOOLEAN DEFAULT FALSE;
    DECLARE v_table_count           INT DEFAULT 0;
    DECLARE v_column_count          INT DEFAULT 0;
    DECLARE v_provenance_count      INT DEFAULT 0;
    DECLARE v_core_run_count        INT DEFAULT 0;
    DECLARE v_lookups_seeded        INT DEFAULT 0;
    DECLARE v_source_case_count     INT DEFAULT 0;
    DECLARE v_cases_no_accident     INT DEFAULT 0;
    DECLARE v_cases_already_correct INT DEFAULT 0;
    DECLARE v_cases_needing_update  INT DEFAULT 0;
    DECLARE v_conflict_count        INT DEFAULT 0;
    DECLARE v_cases_updated         INT DEFAULT 0;
    DECLARE v_preimage_matches      INT DEFAULT 0;
    DECLARE v_postcondition_errors  INT DEFAULT 0;
    DECLARE v_core_run_id           CHAR(36);
    DECLARE v_org_id                CHAR(36);
    DECLARE v_migration_user_id     CHAR(36);
    DECLARE v_source_fingerprint    CHAR(64);
    DECLARE v_legacy_program        VARCHAR(50);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN
            ROLLBACK;
            SET v_in_transaction = FALSE;
        END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_slat_repair;
        IF v_lock_acquired = 1 THEN
            DO RELEASE_LOCK(v_lock_name);
        END IF;
        RESIGNAL;
    END;

    -- -------------------------------------------------------------------------
    -- Parameter validation
    -- -------------------------------------------------------------------------
    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_apply     = (p_apply = '1');
    SET v_lock_name = CONCAT('liens:slcore:', v_tenant_id);

    IF DATABASE() NOT IN ('LS_LIENS', 'LS_QA_LIENS') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTA-001 target schema must be LS_LIENS or LS_QA_LIENS';
    END IF;
    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP
              '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL
       OR p_apply NOT IN ('0', '1')
       OR p_expected_updates IS NULL
       OR (NOT v_apply AND p_expected_updates <> -1)
       OR (v_apply AND p_expected_updates < 0) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT =
                'LSLTA-002 invalid tenant ID, expected update count, or apply flag';
    END IF;

    -- Acquire the same advisory lock used by the core importer so concurrent
    -- runs for the same tenant are blocked.
    SELECT GET_LOCK(v_lock_name, 10) INTO v_lock_acquired;
    IF COALESCE(v_lock_acquired, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT =
                'LSLTA-003 SL-CORE migration or repair is already active for this tenant';
    END IF;

    -- -------------------------------------------------------------------------
    -- Schema contract verification
    -- -------------------------------------------------------------------------
    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE (table_schema = DATABASE()
           AND table_type = 'BASE TABLE'
           AND table_name IN ('liens_Cases', 'liens_LookupValues',
                              'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns'))
       OR (table_schema = 'SL-CORE'
           AND table_type = 'BASE TABLE'
           AND table_name IN ('SL_CASE', 'SL_ACCIDENT_TYPE',
                              'SL_MIGRATION_SOURCE_PROVENANCE'));
    IF v_table_count <> 7 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT =
                'LSLTA-004 required source or target tables are unavailable';
    END IF;

    -- liens_Cases(6) + liens_LookupValues(12) + liens_LegacyIdCrosswalks(7)
    -- + liens_LegacyImportRuns(9) + SL_CASE(4) + SL_ACCIDENT_TYPE(2)
    -- + SL_MIGRATION_SOURCE_PROVENANCE(3) = 43
    SELECT COUNT(*) INTO v_column_count
    FROM information_schema.columns
    WHERE (table_schema = DATABASE() AND (
           (table_name = 'liens_Cases'
            AND column_name IN ('Id','TenantId','OrgId','Notes',
                                'UpdatedAtUtc','UpdatedByUserId'))
        OR (table_name = 'liens_LookupValues'
            AND column_name IN ('Id','TenantId','Category','Code','Name',
                                'IsActive','IsSystem','SortOrder','Description',
                                'CreatedAtUtc','UpdatedAtUtc','CreatedByUserId'))
        OR (table_name = 'liens_LegacyIdCrosswalks'
            AND column_name IN ('TenantId','SourceSystem','SourceTable','LegacyId',
                                'TargetEntity','TargetId','ImportRunId'))
        OR (table_name = 'liens_LegacyImportRuns'
            AND column_name IN ('Id','TenantId','OrgId','SourceSystem',
                                'SourceFingerprint','LegacyProgram','MappingVersion',
                                'Status','CreatedByUserId'))))
       OR (table_schema = 'SL-CORE' AND (
           (table_name = 'SL_CASE'
            AND column_name IN ('CASE_ID','CASE_PROGRAM',
                                'CASE_ACCIDENT_TYPE','CASE_IS_DELETED'))
        OR (table_name = 'SL_ACCIDENT_TYPE'
            AND column_name IN ('AT_ID','AT_DESCRIPTION'))
        OR (table_name = 'SL_MIGRATION_SOURCE_PROVENANCE'
            AND column_name IN ('PROVENANCE_KEY','SOURCE_FINGERPRINT',
                                'IMPORT_SCOPE'))));
    IF v_column_count <> 43 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT =
                'LSLTA-005 required source or target column contract is incomplete';
    END IF;

    -- -------------------------------------------------------------------------
    -- Require exactly one completed Program 1 core import with case crosswalks.
    -- -------------------------------------------------------------------------
    SELECT COUNT(*) INTO v_core_run_count
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId   = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.LegacyProgram = '1'
      AND r.MappingVersion = 'sl-core-core-liens-v1'
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          WHERE x.TenantId    = r.TenantId
            AND x.ImportRunId = r.Id
            AND x.SourceSystem  = 'SL-CORE'
            AND x.SourceTable   = 'SL_CASE'
            AND x.TargetEntity  = 'Case');
    IF v_core_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT =
                'LSLTA-006 exactly one completed Program 1 core import with case crosswalks is required';
    END IF;

    SELECT r.Id, r.OrgId, r.CreatedByUserId,
           LOWER(r.SourceFingerprint), r.LegacyProgram
      INTO v_core_run_id, v_org_id, v_migration_user_id,
           v_source_fingerprint, v_legacy_program
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId   = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.LegacyProgram = '1'
      AND r.MappingVersion = 'sl-core-core-liens-v1'
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          WHERE x.TenantId    = r.TenantId
            AND x.ImportRunId = r.Id
            AND x.SourceSystem  = 'SL-CORE'
            AND x.SourceTable   = 'SL_CASE'
            AND x.TargetEntity  = 'Case');

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current'
      AND LOWER(SOURCE_FINGERPRINT) = v_source_fingerprint
      AND IMPORT_SCOPE = 'sl-core-core-liens-v1';
    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT =
                'LSLTA-007 source provenance does not match the completed core import';
    END IF;

    -- -------------------------------------------------------------------------
    -- Phase 1: Seed missing AccidentType lookup values.
    --
    -- Runs in both preflight and apply so that the staging table built in
    -- Phase 2 reflects the final post-seed state.  The INSERT is idempotent
    -- (WHERE NOT EXISTS guard).  Medical Malpractice (AT_ID=39) is the known
    -- missing entry; extend the candidate rows for any future gaps.
    --
    -- TenantId IS NULL marks system-wide seed values visible to all tenants.
    -- -------------------------------------------------------------------------
    INSERT INTO liens_LookupValues
        (Id, TenantId, Category, Code, Name, Description, SortOrder,
         IsActive, IsSystem, CreatedAtUtc, UpdatedAtUtc,
         CreatedByUserId, UpdatedByUserId)
    SELECT UUID(), NULL, 'AccidentType',
           candidate.Code, candidate.Name, NULL, candidate.SortOrder,
           1, 1,
           UTC_TIMESTAMP(6), UTC_TIMESTAMP(6),
           v_migration_user_id, NULL
    FROM (
        SELECT 'MedicalMalpractice' AS Code,
               'Medical Malpractice' AS Name,
               39 AS SortOrder
        -- Add further rows here if additional AT_IDs are ever found missing.
    ) AS candidate
    WHERE NOT EXISTS (
        SELECT 1
        FROM liens_LookupValues lv
        WHERE lv.TenantId IS NULL
          AND lv.Category  = 'AccidentType'
          AND lv.Code      = candidate.Code
    );
    SET v_lookups_seeded = ROW_COUNT();

    -- -------------------------------------------------------------------------
    -- Phase 2: Build staging table with resolution per source case.
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_slat_repair;

    CREATE TEMPORARY TABLE tmp_slat_repair AS
    SELECT classified.*,
           CASE classified.Resolution
             WHEN 'NeedsUpdate' THEN
               CASE
                 WHEN classified.NotesBefore IS NULL
                      OR TRIM(classified.NotesBefore) = ''
                   THEN CONCAT(
                       '[legacy-meta]', CHAR(10),
                       'accidentTypeId=', classified.TargetLookupId,
                       '; accidentType=', classified.TargetLookupName)
                 WHEN classified.MetadataMarkerCount = 0
                   THEN CONCAT(
                       classified.NotesBefore, CHAR(10), CHAR(10),
                       '[legacy-meta]', CHAR(10),
                       'accidentTypeId=', classified.TargetLookupId,
                       '; accidentType=', classified.TargetLookupName)
                 ELSE
                   -- Marker already present: append to existing metadata line.
                   CONCAT(
                       classified.NotesBefore,
                       '; accidentTypeId=', classified.TargetLookupId,
                       '; accidentType=', classified.TargetLookupName)
               END
             ELSE classified.NotesBefore
           END AS NotesAfter
    FROM (
        SELECT staged.*,
               CASE
                 WHEN staged.CaseCrosswalkId IS NULL
                   THEN 'MissingCaseCrosswalk'
                 WHEN staged.TargetCaseId IS NULL
                      OR staged.TargetCaseOrgId IS NULL
                      OR staged.TargetCaseOrgId <> v_org_id
                   THEN 'InvalidTargetCase'
                 WHEN staged.LegacyAccidentTypeId IS NULL
                   THEN 'NoLegacyAccidentType'
                 WHEN staged.LegacyAccidentTypeName IS NULL
                   THEN 'MissingSourceAccidentType'
                 WHEN staged.LookupMatchCount = 0
                   THEN 'MissingTargetLookup'
                 WHEN staged.LookupMatchCount > 1
                   THEN 'AmbiguousTargetLookup'
                 WHEN staged.MetadataMarkerCount > 1
                   THEN 'AmbiguousLegacyMetadata'
                 -- accidentTypeId= found in Notes but outside a [legacy-meta] block.
                 WHEN staged.MetadataMarkerCount = 0
                      AND LOCATE('accidentTypeId=',
                                 COALESCE(staged.NotesBefore, '')) > 0
                   THEN 'UnmarkedAccidentMetadata'
                 -- Marker present, key present, but UUID could not be extracted.
                 WHEN staged.MetadataMarkerCount = 1
                      AND LOCATE('accidentTypeId=',
                                 staged.MetadataAfterMarker) > 0
                      AND staged.ExistingAccidentTypeId IS NULL
                   THEN 'MalformedExistingAccidentTypeId'
                 -- Existing UUID does not match the computed target UUID.
                 WHEN staged.ExistingAccidentTypeId IS NOT NULL
                      AND LOWER(staged.ExistingAccidentTypeId)
                          <> LOWER(staged.TargetLookupId)
                   THEN 'ConflictingExistingAccidentTypeId'
                 -- Existing UUID already matches: nothing to do.
                 WHEN staged.ExistingAccidentTypeId IS NOT NULL
                   THEN 'AlreadyCorrect'
                 -- Appending would exceed the 4000-char Notes limit.
                 WHEN CHAR_LENGTH(
                     CASE
                       WHEN staged.NotesBefore IS NULL
                            OR TRIM(staged.NotesBefore) = ''
                         THEN CONCAT('[legacy-meta]', CHAR(10),
                                     'accidentTypeId=', staged.TargetLookupId,
                                     '; accidentType=', staged.TargetLookupName)
                       WHEN staged.MetadataMarkerCount = 0
                         THEN CONCAT(staged.NotesBefore, CHAR(10), CHAR(10),
                                     '[legacy-meta]', CHAR(10),
                                     'accidentTypeId=', staged.TargetLookupId,
                                     '; accidentType=', staged.TargetLookupName)
                       ELSE CONCAT(staged.NotesBefore,
                                   '; accidentTypeId=', staged.TargetLookupId,
                                   '; accidentType=', staged.TargetLookupName)
                     END) > 4000
                   THEN 'NotesOverflow'
                 ELSE 'NeedsUpdate'
               END AS Resolution
        FROM (
            SELECT
                src_case.CASE_ID              AS LegacyCaseId,
                src_case.CASE_ACCIDENT_TYPE   AS LegacyAccidentTypeId,
                src_at.AT_DESCRIPTION         AS LegacyAccidentTypeName,
                case_x.Id                     AS CaseCrosswalkId,
                case_x.TargetId               AS TargetCaseId,
                target_case.OrgId             AS TargetCaseOrgId,
                target_case.Notes             AS NotesBefore,
                lv.Id                         AS TargetLookupId,
                lv.Name                       AS TargetLookupName,
                COALESCE(lv_cnt.MatchCount, 0) AS LookupMatchCount,
                -- Count [legacy-meta] markers in Notes.
                (CHAR_LENGTH(COALESCE(target_case.Notes, ''))
                  - CHAR_LENGTH(REPLACE(COALESCE(target_case.Notes, ''),
                                        '[legacy-meta]', '')))
                  / CHAR_LENGTH('[legacy-meta]')
                                               AS MetadataMarkerCount,
                -- Text following the first [legacy-meta] marker.
                CASE
                  WHEN LOCATE('[legacy-meta]',
                              COALESCE(target_case.Notes, '')) > 0
                    THEN SUBSTRING(
                        target_case.Notes,
                        LOCATE('[legacy-meta]', target_case.Notes)
                            + CHAR_LENGTH('[legacy-meta]'))
                  ELSE COALESCE(target_case.Notes, '')
                END                           AS MetadataAfterMarker,
                -- Extract an existing accidentTypeId UUID from the metadata block.
                -- REGEXP_SUBSTR returns NULL when no match; SUBSTRING(NULL,...) = NULL.
                NULLIF(SUBSTRING(
                    REGEXP_SUBSTR(
                        CASE
                          WHEN LOCATE('[legacy-meta]',
                                      COALESCE(target_case.Notes, '')) > 0
                            THEN SUBSTRING(
                                target_case.Notes,
                                LOCATE('[legacy-meta]', target_case.Notes)
                                    + CHAR_LENGTH('[legacy-meta]'))
                          ELSE COALESCE(target_case.Notes, '')
                        END,
                        'accidentTypeId=[0-9A-Fa-f-]{36}'),
                    CHAR_LENGTH('accidentTypeId=') + 1),
                '') AS ExistingAccidentTypeId
            FROM `SL-CORE`.`SL_CASE` src_case
            -- Resolve legacy accident type description.
            LEFT JOIN `SL-CORE`.`SL_ACCIDENT_TYPE` src_at
              ON src_at.AT_ID = src_case.CASE_ACCIDENT_TYPE
            -- Locate the target case via crosswalk.
            LEFT JOIN liens_LegacyIdCrosswalks case_x
              ON case_x.TenantId     = v_tenant_id
             AND case_x.SourceSystem = 'SL-CORE'
             AND case_x.SourceTable  = 'SL_CASE'
             AND case_x.LegacyId     = CAST(src_case.CASE_ID AS CHAR)
             AND case_x.TargetEntity = 'Case'
             AND case_x.ImportRunId  = v_core_run_id
            LEFT JOIN liens_Cases target_case
              ON target_case.Id       = case_x.TargetId
             AND target_case.TenantId = v_tenant_id
            -- Match AT_DESCRIPTION to liens_LookupValues.Name (case-insensitive).
            -- Inactive legacy types are still mapped if a lookup entry exists.
            LEFT JOIN (
                SELECT LOWER(TRIM(Name)) AS NormalizedName,
                       MIN(Id)           AS Id,
                       MIN(Name)         AS Name
                FROM liens_LookupValues
                WHERE TenantId IS NULL
                  AND Category  = 'AccidentType'
                  AND IsActive  = 1
                  AND IsSystem  = 1
                GROUP BY LOWER(TRIM(Name))
            ) lv
              ON lv.NormalizedName = LOWER(TRIM(src_at.AT_DESCRIPTION))
            -- Separate count subquery to detect ambiguous (duplicate) lookup names.
            LEFT JOIN (
                SELECT LOWER(TRIM(Name)) AS NormalizedName,
                       COUNT(*)          AS MatchCount
                FROM liens_LookupValues
                WHERE TenantId IS NULL
                  AND Category  = 'AccidentType'
                  AND IsActive  = 1
                  AND IsSystem  = 1
                GROUP BY LOWER(TRIM(Name))
            ) lv_cnt
              ON lv_cnt.NormalizedName = LOWER(TRIM(src_at.AT_DESCRIPTION))
            WHERE src_case.CASE_PROGRAM   = CAST(v_legacy_program AS UNSIGNED)
              AND COALESCE(src_case.CASE_IS_DELETED, 'N') <> 'Y'
        ) staged
    ) classified;

    -- -------------------------------------------------------------------------
    -- Phase 3: Validation
    -- -------------------------------------------------------------------------
    SELECT COUNT(*) INTO v_source_case_count     FROM tmp_slat_repair;
    SELECT COUNT(*) INTO v_cases_no_accident
        FROM tmp_slat_repair WHERE Resolution = 'NoLegacyAccidentType';
    SELECT COUNT(*) INTO v_cases_already_correct
        FROM tmp_slat_repair WHERE Resolution = 'AlreadyCorrect';
    SELECT COUNT(*) INTO v_cases_needing_update
        FROM tmp_slat_repair WHERE Resolution = 'NeedsUpdate';
    SELECT COUNT(*) INTO v_conflict_count
        FROM tmp_slat_repair
        WHERE Resolution NOT IN ('NoLegacyAccidentType', 'AlreadyCorrect', 'NeedsUpdate');

    IF v_source_case_count = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT =
                'LSLTA-008 no eligible Program 1 source cases were found';
    END IF;

    IF v_conflict_count <> 0 THEN
        -- Emit a breakdown before signaling so the caller can diagnose.
        SELECT Resolution, COUNT(*) AS Cases
        FROM tmp_slat_repair
        WHERE Resolution NOT IN ('NoLegacyAccidentType', 'AlreadyCorrect', 'NeedsUpdate')
        GROUP BY Resolution
        ORDER BY Resolution;

        SELECT LegacyCaseId, TargetCaseId, LegacyAccidentTypeId,
               LegacyAccidentTypeName, TargetLookupId, Resolution
        FROM tmp_slat_repair
        WHERE Resolution NOT IN ('NoLegacyAccidentType', 'AlreadyCorrect', 'NeedsUpdate')
        ORDER BY LegacyCaseId
        LIMIT 100;

        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT =
                'LSLTA-009 source-to-target accident-type mapping conflicts require reconciliation';
    END IF;

    -- -------------------------------------------------------------------------
    -- Phase 4: Report (preflight) or apply (transaction)
    -- -------------------------------------------------------------------------
    IF NOT v_apply THEN
        DROP TEMPORARY TABLE IF EXISTS tmp_slat_repair;
        DO RELEASE_LOCK(v_lock_name);
        SET v_lock_acquired = 0;
        SELECT
            'accident-type-backfill-preflight-passed' AS Result,
            v_core_run_id           AS CoreImportRunId,
            v_source_case_count     AS SourceCases,
            v_cases_no_accident     AS CasesWithoutAccidentType,
            v_cases_already_correct AS CasesAlreadyCorrect,
            v_cases_needing_update  AS CasesNeedingUpdate,
            v_conflict_count        AS Conflicts,
            v_lookups_seeded        AS LookupValuesSeeded;
    ELSE
        IF p_expected_updates <> v_cases_needing_update THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT =
                    'LSLTA-010 expected update count does not match the validated repair plan';
        END IF;

        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        START TRANSACTION;
        SET v_in_transaction = TRUE;

        -- Lock target rows before reading to prevent concurrent modification.
        SELECT COUNT(*) INTO v_preimage_matches
        FROM liens_Cases target_case
        INNER JOIN tmp_slat_repair repair
          ON repair.TargetCaseId = target_case.Id
        WHERE repair.Resolution = 'NeedsUpdate'
          AND target_case.TenantId = v_tenant_id
          AND target_case.OrgId    = v_org_id
          AND target_case.Notes <=> repair.NotesBefore
        FOR UPDATE;

        IF v_preimage_matches <> v_cases_needing_update THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT =
                    'LSLTA-011 row preimage mismatch; re-run preflight before retrying apply';
        END IF;

        UPDATE liens_Cases target_case
        INNER JOIN tmp_slat_repair repair
          ON repair.TargetCaseId = target_case.Id
        SET target_case.Notes           = repair.NotesAfter,
            target_case.UpdatedAtUtc    = UTC_TIMESTAMP(6),
            target_case.UpdatedByUserId = v_migration_user_id
        WHERE repair.Resolution = 'NeedsUpdate'
          AND target_case.TenantId = v_tenant_id
          AND target_case.OrgId    = v_org_id
          AND target_case.Notes <=> repair.NotesBefore;
        SET v_cases_updated = ROW_COUNT();

        IF v_cases_updated <> v_cases_needing_update THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT =
                    'LSLTA-012 update count did not match the validated repair plan';
        END IF;

        -- Postcondition: every updated or already-correct case must contain
        -- the expected accidentTypeId UUID in its Notes.
        SELECT COUNT(*) INTO v_postcondition_errors
        FROM tmp_slat_repair repair
        INNER JOIN liens_Cases target_case
          ON target_case.Id       = repair.TargetCaseId
         AND target_case.TenantId = v_tenant_id
         AND target_case.OrgId    = v_org_id
        WHERE repair.Resolution IN ('NeedsUpdate', 'AlreadyCorrect')
          AND COALESCE(LOCATE(
                  CONCAT('accidentTypeId=', repair.TargetLookupId),
                  target_case.Notes), 0) = 0;
        IF v_postcondition_errors <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT =
                    'LSLTA-013 postcondition failed: accidentTypeId metadata is missing or incorrect';
        END IF;

        COMMIT;
        SET v_in_transaction = FALSE;
        DROP TEMPORARY TABLE IF EXISTS tmp_slat_repair;
        DO RELEASE_LOCK(v_lock_name);
        SET v_lock_acquired = 0;
        SELECT
            'accident-type-backfill-applied' AS Result,
            v_core_run_id           AS CoreImportRunId,
            v_source_case_count     AS SourceCases,
            v_cases_no_accident     AS CasesWithoutAccidentType,
            v_cases_already_correct AS CasesAlreadyCorrect,
            v_cases_updated         AS CasesUpdated,
            v_conflict_count        AS Conflicts,
            v_lookups_seeded        AS LookupValuesSeeded;
    END IF;

    -- Normal exit cleanup (exception handler mirrors this path).
    DROP TEMPORARY TABLE IF EXISTS tmp_slat_repair;
    IF v_lock_acquired = 1 THEN
        DO RELEASE_LOCK(v_lock_name);
    END IF;
END$$

DELIMITER ;

-- Deploy with DBeaver "Execute SQL Script" (Alt+X), not Execute Statement.
--
-- Step 1 – preflight (reports counts, seeds missing lookup values, no case writes):
--   CALL LS_LIENS.liens_backfill_sl_core_case_accident_types(
--       '019f6ae6-4348-784a-aae0-f4d636f843ad', -1, '0');
--
-- Step 2 – apply (use CasesNeedingUpdate from step 1 as the second argument):
--   CALL LS_LIENS.liens_backfill_sl_core_case_accident_types(
--       '019f6ae6-4348-784a-aae0-f4d636f843ad', <CasesNeedingUpdate>, '1');
