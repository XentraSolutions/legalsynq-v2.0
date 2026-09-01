-- Restore plaintiff address fields and law-firm email from a completed SL-CORE import.
--
-- SL_CASE.CASE_ADDRESS/CITY/STATE/ZIPCODE -> liens_Cases client address columns
-- SL_CONTACT.CONTACT_EMAIL (law firms)    -> liens_Contacts.Email
--
-- Usage:
--   CALL liens_backfill_sl_core_plaintiff_address_and_lawfirm_email('<tenant-guid>', -1, '0');
--   CALL liens_backfill_sl_core_plaintiff_address_and_lawfirm_email('<tenant-guid>', <ChangesToApply>, '1');

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;
DROP PROCEDURE IF EXISTS liens_backfill_sl_core_plaintiff_address_and_lawfirm_email;
DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_plaintiff_address_and_lawfirm_email(
    IN p_tenant_id CHAR(36), IN p_expected_changes INT, IN p_apply CHAR(1))
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36); DECLARE v_apply BOOLEAN; DECLARE v_lock_name VARCHAR(64);
    DECLARE v_locked INT DEFAULT 0; DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_core_run_id CHAR(36); DECLARE v_contact_run_id CHAR(36); DECLARE v_org_id CHAR(36);
    DECLARE v_user_id CHAR(36); DECLARE v_program VARCHAR(50); DECLARE v_fingerprint CHAR(64);
    DECLARE v_core_count INT DEFAULT 0; DECLARE v_contact_count INT DEFAULT 0;
    DECLARE v_changes INT DEFAULT 0; DECLARE v_conflicts INT DEFAULT 0; DECLARE v_updated INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
      IF v_in_transaction THEN ROLLBACK; END IF;
      DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_plaintiff_address;
      DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lawfirm_email;
      IF v_locked = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
      RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id)); SET v_apply = p_apply = '1';
    SET v_lock_name = CONCAT('liens:slcore:', v_tenant_id);
    IF DATABASE() NOT IN ('LS_QA_LIENS', 'LS_LIENS') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTPA-001 target schema must be LS_QA_LIENS or LS_LIENS';
    END IF;
    IF v_tenant_id IS NULL OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply NOT IN ('0','1') OR p_expected_changes IS NULL
       OR (NOT v_apply AND p_expected_changes <> -1) OR (v_apply AND p_expected_changes < 0) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTPA-002 invalid tenant ID, expected change count, or apply flag';
    END IF;
    SELECT GET_LOCK(v_lock_name, 10) INTO v_locked;
    IF COALESCE(v_locked, 0) <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTPA-003 SL-CORE import or repair is already active'; END IF;

    SELECT COUNT(*) INTO v_core_count FROM liens_LegacyImportRuns r
    WHERE r.TenantId=v_tenant_id AND r.SourceSystem='SL-CORE' AND r.MappingVersion='sl-core-core-liens-v1' AND r.Status='Completed'
      AND EXISTS (SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.ImportRunId=r.Id AND x.SourceTable='SL_CASE' AND x.TargetEntity='Case');
    IF v_core_count <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTPA-004 exactly one completed SL-CORE core import is required'; END IF;
    SELECT r.Id,r.OrgId,r.CreatedByUserId,r.LegacyProgram,LOWER(r.SourceFingerprint)
      INTO v_core_run_id,v_org_id,v_user_id,v_program,v_fingerprint
    FROM liens_LegacyImportRuns r WHERE r.TenantId=v_tenant_id AND r.SourceSystem='SL-CORE' AND r.MappingVersion='sl-core-core-liens-v1' AND r.Status='Completed';
    SELECT COUNT(*) INTO v_contact_count FROM liens_LegacyImportRuns r
    WHERE r.TenantId=v_tenant_id AND r.OrgId=v_org_id AND r.SourceSystem='SL-CORE' AND r.Status='Completed'
      AND r.MappingVersion IN ('sl-core-contact-facility-v1','sl-core-contact-facility-v2','sl-core-contact-facility-v3')
      AND HEX(LOWER(r.SourceFingerprint))=HEX(v_fingerprint)
      AND EXISTS (SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.ImportRunId=r.Id AND x.SourceTable='SL_CONTACT' AND x.TargetEntity='Contact');
    IF v_contact_count < 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTPA-005 a matching completed law-firm contact import is required'; END IF;
    IF EXISTS (
      SELECT 1 FROM liens_LegacyIdCrosswalks x
      INNER JOIN liens_LegacyImportRuns r ON r.Id=x.ImportRunId
      WHERE x.TenantId=v_tenant_id AND x.SourceSystem='SL-CORE' AND x.SourceTable='SL_CONTACT' AND x.TargetEntity='Contact'
        AND r.OrgId=v_org_id AND r.Status='Completed' AND r.MappingVersion IN ('sl-core-contact-facility-v1','sl-core-contact-facility-v2','sl-core-contact-facility-v3')
        AND HEX(LOWER(r.SourceFingerprint))=HEX(v_fingerprint)
      GROUP BY x.LegacyId HAVING COUNT(DISTINCT x.TargetId) > 1
    ) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTPA-005 law-firm contact crosswalks disagree across completed imports'; END IF;
    IF NOT EXISTS (SELECT 1 FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE` p WHERE p.PROVENANCE_KEY='sl-core-current'
        AND HEX(LOWER(p.SOURCE_FINGERPRINT))=HEX(v_fingerprint) AND HEX(p.IMPORT_SCOPE)=HEX('sl-core-core-liens-v1')) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTPA-006 source provenance does not match the completed core import';
    END IF;

    CREATE TEMPORARY TABLE tmp_sl_core_plaintiff_address AS
    SELECT c.Id TargetId, LEFT(NULLIF(TRIM(s.CASE_ADDRESS),''),300) AddressLine1,
           LEFT(NULLIF(TRIM(s.CASE_CITY),''),100) City, LEFT(NULLIF(TRIM(s.CASE_STATE),''),100) State,
           LEFT(NULLIF(TRIM(s.CASE_ZIPCODE),''),20) PostalCode,
           NULLIF(CONCAT_WS(', ', NULLIF(TRIM(s.CASE_ADDRESS),''), NULLIF(TRIM(s.CASE_CITY),''),
               NULLIF(TRIM(s.CASE_STATE),''), NULLIF(TRIM(s.CASE_ZIPCODE),'')), '') FullAddress,
           CASE WHEN c.Id IS NULL OR c.TenantId<>v_tenant_id OR c.OrgId<>v_org_id THEN 'InvalidTarget'
             WHEN (NULLIF(TRIM(s.CASE_ADDRESS),'') IS NOT NULL AND c.ClientAddressLine1 IS NOT NULL AND HEX(c.ClientAddressLine1)<>HEX(LEFT(TRIM(s.CASE_ADDRESS),300)))
               OR (NULLIF(TRIM(s.CASE_CITY),'') IS NOT NULL AND c.ClientCity IS NOT NULL AND HEX(c.ClientCity)<>HEX(LEFT(TRIM(s.CASE_CITY),100)))
               OR (NULLIF(TRIM(s.CASE_STATE),'') IS NOT NULL AND c.ClientState IS NOT NULL AND HEX(c.ClientState)<>HEX(LEFT(TRIM(s.CASE_STATE),100)))
               OR (NULLIF(TRIM(s.CASE_ZIPCODE),'') IS NOT NULL AND c.ClientPostalCode IS NOT NULL AND HEX(c.ClientPostalCode)<>HEX(LEFT(TRIM(s.CASE_ZIPCODE),20))) THEN 'Conflict'
             WHEN (NULLIF(TRIM(s.CASE_ADDRESS),'') IS NOT NULL AND (c.ClientAddressLine1 IS NULL OR TRIM(c.ClientAddressLine1)=''))
               OR (NULLIF(TRIM(s.CASE_CITY),'') IS NOT NULL AND (c.ClientCity IS NULL OR TRIM(c.ClientCity)=''))
               OR (NULLIF(TRIM(s.CASE_STATE),'') IS NOT NULL AND (c.ClientState IS NULL OR TRIM(c.ClientState)=''))
               OR (NULLIF(TRIM(s.CASE_ZIPCODE),'') IS NOT NULL AND (c.ClientPostalCode IS NULL OR TRIM(c.ClientPostalCode)=''))
               OR (NULLIF(CONCAT_WS(', ', NULLIF(TRIM(s.CASE_ADDRESS),''), NULLIF(TRIM(s.CASE_CITY),''), NULLIF(TRIM(s.CASE_STATE),''), NULLIF(TRIM(s.CASE_ZIPCODE),'')), '') IS NOT NULL
                   AND (c.ClientAddress IS NULL OR TRIM(c.ClientAddress)='')) THEN 'NeedsUpdate'
             ELSE 'AlreadyCorrect' END Resolution
    FROM `SL-CORE`.`SL_CASE` s INNER JOIN liens_LegacyIdCrosswalks x ON x.TenantId=v_tenant_id AND x.ImportRunId=v_core_run_id
      AND x.SourceSystem='SL-CORE' AND x.SourceTable='SL_CASE' AND x.TargetEntity='Case' AND HEX(x.LegacyId)=HEX(CAST(s.CASE_ID AS CHAR))
    LEFT JOIN liens_Cases c ON c.Id=x.TargetId
    WHERE HEX(CAST(s.CASE_PROGRAM AS CHAR))=HEX(v_program) AND UPPER(TRIM(COALESCE(s.CASE_IS_DELETED,'N'))) <> 'Y';

    CREATE TEMPORARY TABLE tmp_sl_core_lawfirm_email AS
    SELECT DISTINCT c.Id TargetId, LEFT(NULLIF(TRIM(s.CONTACT_EMAIL),''),200) SourceEmail,
           CASE WHEN c.Id IS NULL OR c.TenantId<>v_tenant_id OR c.OrgId<>v_org_id THEN 'InvalidTarget'
             WHEN NULLIF(TRIM(s.CONTACT_EMAIL),'') IS NOT NULL AND c.Email IS NOT NULL AND HEX(c.Email)<>HEX(LEFT(TRIM(s.CONTACT_EMAIL),200)) THEN 'Conflict'
             WHEN NULLIF(TRIM(s.CONTACT_EMAIL),'') IS NOT NULL AND (c.Email IS NULL OR TRIM(c.Email)='') THEN 'NeedsUpdate'
             ELSE 'AlreadyCorrect' END Resolution
    FROM `SL-CORE`.`SL_CONTACT` s INNER JOIN liens_LegacyIdCrosswalks x
      ON x.TenantId=v_tenant_id AND x.SourceSystem='SL-CORE' AND x.SourceTable='SL_CONTACT' AND x.TargetEntity='Contact'
     AND HEX(x.LegacyId)=HEX(CAST(s.CONTACT_ID AS CHAR))
     AND EXISTS (SELECT 1 FROM liens_LegacyImportRuns r WHERE r.Id=x.ImportRunId AND r.OrgId=v_org_id
       AND r.Status='Completed' AND r.MappingVersion IN ('sl-core-contact-facility-v1','sl-core-contact-facility-v2','sl-core-contact-facility-v3')
       AND HEX(LOWER(r.SourceFingerprint))=HEX(v_fingerprint))
    LEFT JOIN liens_Contacts c ON c.Id=x.TargetId
    WHERE HEX(CAST(s.CONTACT_PROGRAM AS CHAR))=HEX(v_program) AND s.CONTACT_TYPE=1 AND COALESCE(s.CONTACT_STATUS,'A')='A';

    SELECT COUNT(*) INTO v_changes FROM (SELECT TargetId FROM tmp_sl_core_plaintiff_address WHERE Resolution='NeedsUpdate' UNION ALL SELECT TargetId FROM tmp_sl_core_lawfirm_email WHERE Resolution='NeedsUpdate') q;
    SELECT COUNT(*) INTO v_conflicts FROM (SELECT TargetId FROM tmp_sl_core_plaintiff_address WHERE Resolution IN ('InvalidTarget','Conflict') UNION ALL SELECT TargetId FROM tmp_sl_core_lawfirm_email WHERE Resolution IN ('InvalidTarget','Conflict')) q;
    IF NOT v_apply THEN
      SELECT 'PlaintiffAddress' EntityType,Resolution,COUNT(*) RowCount FROM tmp_sl_core_plaintiff_address GROUP BY Resolution
      UNION ALL SELECT 'LawFirmEmail',Resolution,COUNT(*) FROM tmp_sl_core_lawfirm_email GROUP BY Resolution;
      SELECT v_changes ChangesToApply,v_conflicts Conflicts;
    ELSE
      IF p_expected_changes<>v_changes THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTPA-007 expected change count does not match dry run'; END IF;
      IF v_conflicts<>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTPA-008 plaintiff-address/law-firm-email backfill has conflicts; no rows were changed'; END IF;
      START TRANSACTION; SET v_in_transaction=TRUE;
      UPDATE liens_Cases c INNER JOIN tmp_sl_core_plaintiff_address s ON s.TargetId=c.Id AND s.Resolution='NeedsUpdate'
      SET c.ClientAddress=COALESCE(NULLIF(TRIM(c.ClientAddress),''),s.FullAddress),c.ClientAddressLine1=COALESCE(NULLIF(TRIM(c.ClientAddressLine1),''),s.AddressLine1),c.ClientCity=COALESCE(NULLIF(TRIM(c.ClientCity),''),s.City),c.ClientState=COALESCE(NULLIF(TRIM(c.ClientState),''),s.State),c.ClientPostalCode=COALESCE(NULLIF(TRIM(c.ClientPostalCode),''),s.PostalCode),c.UpdatedAtUtc=UTC_TIMESTAMP(6),c.UpdatedByUserId=v_user_id;
      SET v_updated=ROW_COUNT();
      UPDATE liens_Contacts c INNER JOIN tmp_sl_core_lawfirm_email s ON s.TargetId=c.Id AND s.Resolution='NeedsUpdate'
      SET c.Email=COALESCE(NULLIF(TRIM(c.Email),''),s.SourceEmail),c.UpdatedAtUtc=UTC_TIMESTAMP(6),c.UpdatedByUserId=v_user_id;
      SET v_updated=v_updated+ROW_COUNT();
      IF v_updated<>v_changes THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTPA-009 backfill updated-row count failed'; END IF;
      COMMIT; SET v_in_transaction=FALSE; SELECT v_updated RowsUpdated,v_changes ExpectedRowsUpdated;
    END IF;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_plaintiff_address; DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lawfirm_email; DO RELEASE_LOCK(v_lock_name);
END$$
DELIMITER ;
