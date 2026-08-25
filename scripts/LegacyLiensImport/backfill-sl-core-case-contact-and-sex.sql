-- Restore case phone, email, and sex from a completed SL-CORE core import.
--
-- SL_CASE.CASE_PHONE  -> liens_Cases.ClientPhone
-- SL_CASE.CASE_EMAIL  -> liens_Cases.ClientEmail
-- SL_CASE.CASE_GENDER -> liens_Cases.Notes [legacy-meta] gender
--
-- Usage:
--   CALL liens_backfill_sl_core_case_contact_and_sex('<tenant-guid>', -1, '0');
--   CALL liens_backfill_sl_core_case_contact_and_sex('<tenant-guid>', <ChangesToApply>, '1');

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;
DROP PROCEDURE IF EXISTS liens_backfill_sl_core_case_contact_and_sex;
DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_case_contact_and_sex(
    IN p_tenant_id CHAR(36), IN p_expected_changes INT, IN p_apply CHAR(1))
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36); DECLARE v_apply BOOLEAN; DECLARE v_lock_name VARCHAR(64);
    DECLARE v_locked INT DEFAULT 0; DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_core_run_id CHAR(36); DECLARE v_org_id CHAR(36); DECLARE v_user_id CHAR(36);
    DECLARE v_program VARCHAR(50); DECLARE v_fingerprint CHAR(64);
    DECLARE v_core_count INT DEFAULT 0; DECLARE v_changes INT DEFAULT 0;
    DECLARE v_conflicts INT DEFAULT 0; DECLARE v_updated INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
      IF v_in_transaction THEN ROLLBACK; END IF;
      DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_contact_and_sex;
      IF v_locked = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
      RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id)); SET v_apply = p_apply = '1';
    SET v_lock_name = CONCAT('liens:slcore:', v_tenant_id);
    IF DATABASE() NOT IN ('LS_QA_LIENS', 'LS_LIENS') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCS-001 target schema must be LS_QA_LIENS or LS_LIENS';
    END IF;
    IF v_tenant_id IS NULL OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply NOT IN ('0','1') OR p_expected_changes IS NULL
       OR (NOT v_apply AND p_expected_changes <> -1) OR (v_apply AND p_expected_changes < 0) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCS-002 invalid tenant ID, expected change count, or apply flag';
    END IF;
    SELECT GET_LOCK(v_lock_name, 10) INTO v_locked;
    IF COALESCE(v_locked, 0) <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCS-003 SL-CORE import or repair is already active'; END IF;

    SELECT COUNT(*) INTO v_core_count FROM liens_LegacyImportRuns r
    WHERE r.TenantId=v_tenant_id AND r.SourceSystem='SL-CORE' AND r.MappingVersion='sl-core-core-liens-v1' AND r.Status='Completed'
      AND EXISTS (SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.ImportRunId=r.Id AND x.SourceTable='SL_CASE' AND x.TargetEntity='Case');
    IF v_core_count <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCS-004 exactly one completed SL-CORE core import is required'; END IF;
    SELECT r.Id,r.OrgId,r.CreatedByUserId,r.LegacyProgram,LOWER(r.SourceFingerprint)
      INTO v_core_run_id,v_org_id,v_user_id,v_program,v_fingerprint
    FROM liens_LegacyImportRuns r WHERE r.TenantId=v_tenant_id AND r.SourceSystem='SL-CORE' AND r.MappingVersion='sl-core-core-liens-v1' AND r.Status='Completed';
    IF NOT EXISTS (SELECT 1 FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE` p WHERE p.PROVENANCE_KEY='sl-core-current'
        AND HEX(LOWER(p.SOURCE_FINGERPRINT))=HEX(v_fingerprint) AND HEX(p.IMPORT_SCOPE)=HEX('sl-core-core-liens-v1')) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCS-005 source provenance does not match the completed core import';
    END IF;

    CREATE TEMPORARY TABLE tmp_sl_core_case_contact_and_sex AS
    SELECT staged.*, CASE
      WHEN SourcePhone IS NULL AND SourceEmail IS NULL AND SourceSex IS NULL THEN 'NoSourceContactOrSex'
      WHEN TargetId IS NULL OR TargetTenantId<>v_tenant_id OR TargetOrgId<>v_org_id THEN 'InvalidTarget'
      WHEN SourcePhone IS NOT NULL AND ExistingPhone IS NOT NULL AND HEX(ExistingPhone)<>HEX(SourcePhone) THEN 'PhoneConflict'
      WHEN SourceEmail IS NOT NULL AND ExistingEmail IS NOT NULL AND HEX(ExistingEmail)<>HEX(SourceEmail) THEN 'EmailConflict'
      WHEN SourceSex IS NOT NULL AND ExistingSex IS NOT NULL AND HEX(LOWER(ExistingSex))<>HEX(LOWER(SourceSex)) THEN 'SexConflict'
      WHEN (SourcePhone IS NOT NULL AND (ExistingPhone IS NULL OR TRIM(ExistingPhone)=''))
        OR (SourceEmail IS NOT NULL AND (ExistingEmail IS NULL OR TRIM(ExistingEmail)=''))
        OR (SourceSex IS NOT NULL AND ExistingSex IS NULL) THEN 'NeedsUpdate'
      ELSE 'AlreadyCorrect' END Resolution
    FROM (
      SELECT c.Id TargetId,c.TenantId TargetTenantId,c.OrgId TargetOrgId,c.ClientPhone ExistingPhone,c.ClientEmail ExistingEmail,c.Notes ExistingNotes,
        LEFT(NULLIF(TRIM(s.CASE_PHONE),''),30) SourcePhone,LEFT(NULLIF(TRIM(s.CASE_EMAIL),''),320) SourceEmail,
        LEFT(NULLIF(TRIM(s.CASE_GENDER),''),100) SourceSex,
        NULLIF(TRIM(CASE WHEN LOCATE('gender=',LOWER(COALESCE(c.Notes,'')))=0 THEN NULL
          ELSE SUBSTRING_INDEX(SUBSTRING(c.Notes,LOCATE('gender=',LOWER(c.Notes))+7),';',1) END),'') ExistingSex
      FROM `SL-CORE`.`SL_CASE` s INNER JOIN liens_LegacyIdCrosswalks x ON x.TenantId=v_tenant_id AND x.ImportRunId=v_core_run_id
        AND x.SourceSystem='SL-CORE' AND x.SourceTable='SL_CASE' AND x.TargetEntity='Case' AND HEX(x.LegacyId)=HEX(CAST(s.CASE_ID AS CHAR))
      LEFT JOIN liens_Cases c ON c.Id=x.TargetId
      WHERE HEX(CAST(s.CASE_PROGRAM AS CHAR))=HEX(v_program) AND UPPER(TRIM(COALESCE(s.CASE_IS_DELETED,'N'))) <> 'Y'
    ) staged;

    SELECT COUNT(*) INTO v_changes FROM tmp_sl_core_case_contact_and_sex WHERE Resolution='NeedsUpdate';
    SELECT COUNT(*) INTO v_conflicts FROM tmp_sl_core_case_contact_and_sex WHERE Resolution IN ('InvalidTarget','PhoneConflict','EmailConflict','SexConflict');
    IF NOT v_apply THEN
      SELECT 'CaseContactAndSex' EntityType,Resolution,COUNT(*) RowCount FROM tmp_sl_core_case_contact_and_sex GROUP BY Resolution;
      SELECT v_changes ChangesToApply,v_conflicts Conflicts;
    ELSE
      IF p_expected_changes<>v_changes THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTCS-006 expected change count does not match dry run'; END IF;
      IF v_conflicts<>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTCS-007 case contact/sex backfill has conflicts; no rows were changed'; END IF;
      START TRANSACTION; SET v_in_transaction=TRUE;
      UPDATE liens_Cases c INNER JOIN tmp_sl_core_case_contact_and_sex s ON s.TargetId=c.Id AND s.Resolution='NeedsUpdate'
      SET c.ClientPhone=COALESCE(NULLIF(TRIM(c.ClientPhone),''),s.SourcePhone),
          c.ClientEmail=COALESCE(NULLIF(TRIM(c.ClientEmail),''),s.SourceEmail),
          c.Notes=CASE WHEN s.SourceSex IS NULL OR s.ExistingSex IS NOT NULL THEN c.Notes
            WHEN c.Notes IS NULL OR TRIM(c.Notes)='' THEN CONCAT('[legacy-meta]',CHAR(10),'gender=',s.SourceSex)
            WHEN LOCATE('[legacy-meta]',c.Notes)=0 THEN CONCAT(c.Notes,CHAR(10),CHAR(10),'[legacy-meta]',CHAR(10),'gender=',s.SourceSex)
            ELSE CONCAT(c.Notes,'; gender=',s.SourceSex) END,
          c.UpdatedAtUtc=UTC_TIMESTAMP(6),c.UpdatedByUserId=v_user_id;
      SET v_updated=ROW_COUNT();
      IF v_updated<>v_changes THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTCS-008 backfill updated-row count failed'; END IF;
      COMMIT; SET v_in_transaction=FALSE; SELECT v_updated RowsUpdated,v_changes ExpectedRowsUpdated;
    END IF;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_contact_and_sex; DO RELEASE_LOCK(v_lock_name);
END$$
DELIMITER ;
