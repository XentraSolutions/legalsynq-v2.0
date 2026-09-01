-- Restore report-party and medical-facility details from completed SL-CORE imports.
--
-- CASE_MANAGER / CASE_TRACKING_CONTACT (+ email) -> caseManagerId metadata + case-manager contact
-- LAW_FIRM phone/address/city/state/zip              -> law-firm contact
-- MEDICAL_FACILITY address/city/state/zip            -> facility + lien FacilityId
--
-- Usage:
--   CALL liens_backfill_sl_core_report_party_and_facility_details('<tenant-guid>', -1, '0');
--   CALL liens_backfill_sl_core_report_party_and_facility_details('<tenant-guid>', <ChangesToApply>, '1');

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;
DROP PROCEDURE IF EXISTS liens_backfill_sl_core_report_party_and_facility_details;
DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_report_party_and_facility_details(
    IN p_tenant_id CHAR(36), IN p_expected_changes INT, IN p_apply CHAR(1))
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36); DECLARE v_apply BOOLEAN; DECLARE v_lock_name VARCHAR(64);
    DECLARE v_locked INT DEFAULT 0; DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_core_run_id CHAR(36); DECLARE v_org_id CHAR(36); DECLARE v_user_id CHAR(36);
    DECLARE v_program VARCHAR(50); DECLARE v_fingerprint CHAR(64); DECLARE v_core_count INT DEFAULT 0;
    DECLARE v_changes INT DEFAULT 0; DECLARE v_conflicts INT DEFAULT 0; DECLARE v_updated INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
      IF v_in_transaction THEN ROLLBACK; END IF;
      DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_report_case_managers;
      DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_report_law_firms;
      DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_report_facilities;
      IF v_locked = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
      RESIGNAL;
    END;

    SET v_tenant_id=LOWER(TRIM(p_tenant_id)); SET v_apply=p_apply='1'; SET v_lock_name=CONCAT('liens:slcore:',v_tenant_id);
    IF DATABASE() NOT IN ('LS_QA_LIENS','LS_LIENS') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTRPF-001 target schema must be LS_QA_LIENS or LS_LIENS'; END IF;
    IF v_tenant_id IS NULL OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply NOT IN ('0','1') OR p_expected_changes IS NULL
       OR (NOT v_apply AND p_expected_changes<>-1) OR (v_apply AND p_expected_changes<0) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTRPF-002 invalid tenant ID, expected change count, or apply flag';
    END IF;
    SELECT GET_LOCK(v_lock_name,10) INTO v_locked;
    IF COALESCE(v_locked,0)<>1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTRPF-003 SL-CORE import or repair is already active'; END IF;
    SELECT COUNT(*) INTO v_core_count FROM liens_LegacyImportRuns r WHERE r.TenantId=v_tenant_id AND r.SourceSystem='SL-CORE'
      AND r.MappingVersion='sl-core-core-liens-v1' AND r.Status='Completed'
      AND EXISTS(SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.ImportRunId=r.Id AND x.SourceTable='SL_CASE' AND x.TargetEntity='Case');
    IF v_core_count<>1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTRPF-004 exactly one completed SL-CORE core import is required'; END IF;
    SELECT r.Id,r.OrgId,r.CreatedByUserId,r.LegacyProgram,LOWER(r.SourceFingerprint) INTO v_core_run_id,v_org_id,v_user_id,v_program,v_fingerprint
    FROM liens_LegacyImportRuns r WHERE r.TenantId=v_tenant_id AND r.SourceSystem='SL-CORE' AND r.MappingVersion='sl-core-core-liens-v1' AND r.Status='Completed';
    IF NOT EXISTS(SELECT 1 FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE` p WHERE p.PROVENANCE_KEY='sl-core-current'
      AND HEX(LOWER(p.SOURCE_FINGERPRINT))=HEX(v_fingerprint) AND HEX(p.IMPORT_SCOPE)=HEX('sl-core-core-liens-v1')) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTRPF-005 source provenance does not match the completed core import';
    END IF;

    -- One mapped case-manager source row per case. Its linked contact supplies both manager email fields.
    CREATE TEMPORARY TABLE tmp_sl_core_report_case_managers AS
    SELECT q.*,CASE
      WHEN TargetCaseId IS NULL OR TargetCaseTenantId<>v_tenant_id OR TargetCaseOrgId<>v_org_id OR TargetContactId IS NULL OR TargetContactTenantId<>v_tenant_id OR TargetContactOrgId<>v_org_id THEN 'InvalidTarget'
      WHEN ExistingManagerId IS NOT NULL AND HEX(ExistingManagerId)<>HEX(TargetContactId) THEN 'CaseManagerConflict'
      WHEN SourceEmail IS NOT NULL AND ExistingEmail IS NOT NULL AND HEX(ExistingEmail)<>HEX(SourceEmail) THEN 'CaseManagerEmailConflict'
      WHEN ExistingManagerId IS NULL OR (SourceEmail IS NOT NULL AND (ExistingEmail IS NULL OR TRIM(ExistingEmail)='')) THEN 'NeedsUpdate'
      ELSE 'AlreadyCorrect' END Resolution
    FROM (
      SELECT c.Id TargetCaseId,c.TenantId TargetCaseTenantId,c.OrgId TargetCaseOrgId,c.Notes ExistingNotes,
        NULLIF(TRIM(CASE WHEN LOCATE('casemanagerid=',LOWER(COALESCE(c.Notes,'')))=0 THEN NULL ELSE SUBSTRING_INDEX(SUBSTRING(c.Notes,LOCATE('casemanagerid=',LOWER(c.Notes))+14),';',1) END),'') ExistingManagerId,
        cmx.TargetId TargetContactId,mc.TenantId TargetContactTenantId,mc.OrgId TargetContactOrgId,mc.Email ExistingEmail,LEFT(NULLIF(TRIM(cm.CM_EMAIL),''),320) SourceEmail
      FROM `SL-CORE`.`SL_CASE` sc JOIN liens_LegacyIdCrosswalks cx ON cx.TenantId=v_tenant_id AND cx.ImportRunId=v_core_run_id AND cx.SourceTable='SL_CASE' AND cx.TargetEntity='Case' AND HEX(cx.LegacyId)=HEX(CAST(sc.CASE_ID AS CHAR))
      LEFT JOIN liens_Cases c ON c.Id=cx.TargetId LEFT JOIN `SL-CORE`.`SL_CASE_MANAGER` cm ON cm.CM_ID=sc.CASE_MANAGER
      LEFT JOIN liens_LegacyIdCrosswalks cmx ON cmx.TenantId=v_tenant_id AND cmx.SourceSystem='SL-CORE' AND cmx.SourceTable='SL_CASE_MANAGER' AND cmx.TargetEntity='Contact' AND HEX(cmx.LegacyId)=HEX(CAST(cm.CM_ID AS CHAR))
        AND EXISTS(SELECT 1 FROM liens_LegacyImportRuns r WHERE r.Id=cmx.ImportRunId AND r.OrgId=v_org_id AND r.Status='Completed' AND r.MappingVersion IN ('sl-core-contact-facility-v2','sl-core-contact-facility-v3') AND HEX(LOWER(r.SourceFingerprint))=HEX(v_fingerprint))
      LEFT JOIN liens_Contacts mc ON mc.Id=cmx.TargetId
      WHERE HEX(CAST(sc.CASE_PROGRAM AS CHAR))=HEX(v_program) AND UPPER(TRIM(COALESCE(sc.CASE_IS_DELETED,'N'))) <> 'Y' AND sc.CASE_MANAGER IS NOT NULL
    ) q;

    -- The law firm is a type-1 SL_CONTACT. The report reads the linked V3 contact's details.
    CREATE TEMPORARY TABLE tmp_sl_core_report_law_firms AS
    SELECT q.*,CASE
      WHEN TargetContactId IS NULL OR TargetTenantId<>v_tenant_id OR TargetOrgId<>v_org_id THEN 'InvalidTarget'
      WHEN (SourcePhone IS NOT NULL AND ExistingPhone IS NOT NULL AND HEX(ExistingPhone)<>HEX(SourcePhone)) OR (SourceAddress IS NOT NULL AND ExistingAddress IS NOT NULL AND HEX(ExistingAddress)<>HEX(SourceAddress)) OR (SourceCity IS NOT NULL AND ExistingCity IS NOT NULL AND HEX(ExistingCity)<>HEX(SourceCity)) OR (SourceState IS NOT NULL AND ExistingState IS NOT NULL AND HEX(ExistingState)<>HEX(SourceState)) OR (SourceZip IS NOT NULL AND ExistingZip IS NOT NULL AND HEX(ExistingZip)<>HEX(SourceZip)) THEN 'LawFirmDetailConflict'
      WHEN (SourcePhone IS NOT NULL AND (ExistingPhone IS NULL OR TRIM(ExistingPhone)='')) OR (SourceAddress IS NOT NULL AND (ExistingAddress IS NULL OR TRIM(ExistingAddress)='')) OR (SourceCity IS NOT NULL AND (ExistingCity IS NULL OR TRIM(ExistingCity)='')) OR (SourceState IS NOT NULL AND (ExistingState IS NULL OR TRIM(ExistingState)='')) OR (SourceZip IS NOT NULL AND (ExistingZip IS NULL OR TRIM(ExistingZip)='')) THEN 'NeedsUpdate'
      ELSE 'AlreadyCorrect' END Resolution
    FROM (
      SELECT DISTINCT x.TargetId TargetContactId,c.TenantId TargetTenantId,c.OrgId TargetOrgId,c.Phone ExistingPhone,c.AddressLine1 ExistingAddress,c.City ExistingCity,c.State ExistingState,c.PostalCode ExistingZip,
        LEFT(NULLIF(TRIM(s.CONTACT_PHONE),''),30) SourcePhone,LEFT(NULLIF(TRIM(s.CONTACT_ADDRESS),''),300) SourceAddress,LEFT(NULLIF(TRIM(s.CONTACT_CITY),''),100) SourceCity,LEFT(NULLIF(TRIM(s.CONTACT_STATE),''),100) SourceState,LEFT(NULLIF(TRIM(s.CONTACT_ZIP),''),20) SourceZip
      FROM `SL-CORE`.`SL_CONTACT` s JOIN liens_LegacyIdCrosswalks x ON x.TenantId=v_tenant_id AND x.SourceSystem='SL-CORE' AND x.SourceTable='SL_CONTACT' AND x.TargetEntity='Contact' AND HEX(x.LegacyId)=HEX(CAST(s.CONTACT_ID AS CHAR))
        AND EXISTS(SELECT 1 FROM liens_LegacyImportRuns r WHERE r.Id=x.ImportRunId AND r.OrgId=v_org_id AND r.Status='Completed' AND r.MappingVersion IN ('sl-core-contact-facility-v1','sl-core-contact-facility-v2','sl-core-contact-facility-v3') AND HEX(LOWER(r.SourceFingerprint))=HEX(v_fingerprint))
      LEFT JOIN liens_Contacts c ON c.Id=x.TargetId WHERE HEX(CAST(s.CONTACT_PROGRAM AS CHAR))=HEX(v_program) AND s.CONTACT_TYPE=1 AND COALESCE(s.CONTACT_STATUS,'A')='A'
    ) q;

    -- Each linked facility is copied to the V3 facility and is attached to its blank V3 lien.
    CREATE TEMPORARY TABLE tmp_sl_core_report_facilities AS
    SELECT q.*,CASE
      WHEN TargetLienId IS NULL OR TargetLienTenantId<>v_tenant_id OR TargetLienOrgId<>v_org_id OR TargetFacilityId IS NULL OR TargetFacilityTenantId<>v_tenant_id OR TargetFacilityOrgId<>v_org_id THEN 'InvalidTarget'
      WHEN ExistingLienFacilityId IS NOT NULL AND ExistingLienFacilityId<>TargetFacilityId THEN 'LienFacilityConflict'
      WHEN (SourceAddress IS NOT NULL AND ExistingAddress IS NOT NULL AND HEX(ExistingAddress)<>HEX(SourceAddress)) OR (SourceCity IS NOT NULL AND ExistingCity IS NOT NULL AND HEX(ExistingCity)<>HEX(SourceCity)) OR (SourceState IS NOT NULL AND ExistingState IS NOT NULL AND HEX(ExistingState)<>HEX(SourceState)) OR (SourceZip IS NOT NULL AND ExistingZip IS NOT NULL AND HEX(ExistingZip)<>HEX(SourceZip)) THEN 'MedicalFacilityDetailConflict'
      WHEN ExistingLienFacilityId IS NULL OR (SourceAddress IS NOT NULL AND (ExistingAddress IS NULL OR TRIM(ExistingAddress)='')) OR (SourceCity IS NOT NULL AND (ExistingCity IS NULL OR TRIM(ExistingCity)='')) OR (SourceState IS NOT NULL AND (ExistingState IS NULL OR TRIM(ExistingState)='')) OR (SourceZip IS NOT NULL AND (ExistingZip IS NULL OR TRIM(ExistingZip)='')) THEN 'NeedsUpdate'
      ELSE 'AlreadyCorrect' END Resolution
    FROM (
      SELECT DISTINCT l.Id TargetLienId,l.TenantId TargetLienTenantId,l.OrgId TargetLienOrgId,l.FacilityId ExistingLienFacilityId,fx.TargetId TargetFacilityId,f.TenantId TargetFacilityTenantId,f.OrgId TargetFacilityOrgId,f.AddressLine1 ExistingAddress,f.City ExistingCity,f.State ExistingState,f.PostalCode ExistingZip,
        LEFT(NULLIF(TRIM(sf.FACILITY_ADDRESS),''),300) SourceAddress,LEFT(NULLIF(TRIM(sf.FACILITY_CITY),''),100) SourceCity,LEFT(NULLIF(TRIM(sf.FACILITY_STATE),''),100) SourceState,LEFT(NULLIF(TRIM(sf.FACILITY_ZIP),''),20) SourceZip
      FROM `SL-CORE`.`SL_LEINS_MEDICAL_INFORMATION_FACILITY` link JOIN liens_LegacyIdCrosswalks lx ON lx.TenantId=v_tenant_id AND lx.ImportRunId=v_core_run_id AND lx.SourceTable='SL_LEINS_MEDICAL' AND lx.TargetEntity='Lien' AND HEX(lx.LegacyId)=HEX(CAST(link.LMI_LM_ID AS CHAR))
      LEFT JOIN liens_Liens l ON l.Id=lx.TargetId LEFT JOIN `SL-CORE`.`SL_FACILITY` sf ON sf.FACILITY_ID=link.LMI_FACILITY_ID
      LEFT JOIN liens_LegacyIdCrosswalks fx ON fx.TenantId=v_tenant_id AND fx.SourceSystem='SL-CORE' AND fx.SourceTable='SL_FACILITY' AND fx.TargetEntity='Facility' AND HEX(fx.LegacyId)=HEX(CAST(sf.FACILITY_ID AS CHAR))
        AND EXISTS(SELECT 1 FROM liens_LegacyImportRuns r WHERE r.Id=fx.ImportRunId AND r.OrgId=v_org_id AND r.Status='Completed' AND r.MappingVersion IN ('sl-core-contact-facility-v1','sl-core-contact-facility-v2','sl-core-contact-facility-v3') AND HEX(LOWER(r.SourceFingerprint))=HEX(v_fingerprint))
      LEFT JOIN liens_Facilities f ON f.Id=fx.TargetId WHERE HEX(CAST(sf.FACILITY_PROGRAM AS CHAR))=HEX(v_program) AND COALESCE(sf.FACILITY_STATUS,'A')='A'
    ) q;

    SELECT COUNT(*) INTO v_changes FROM (SELECT TargetCaseId Id FROM tmp_sl_core_report_case_managers WHERE Resolution='NeedsUpdate' UNION ALL SELECT TargetContactId FROM tmp_sl_core_report_law_firms WHERE Resolution='NeedsUpdate' UNION ALL SELECT TargetLienId FROM tmp_sl_core_report_facilities WHERE Resolution='NeedsUpdate') q;
    SELECT COUNT(*) INTO v_conflicts FROM (SELECT TargetCaseId Id FROM tmp_sl_core_report_case_managers WHERE Resolution NOT IN ('NeedsUpdate','AlreadyCorrect') UNION ALL SELECT TargetContactId FROM tmp_sl_core_report_law_firms WHERE Resolution NOT IN ('NeedsUpdate','AlreadyCorrect') UNION ALL SELECT TargetLienId FROM tmp_sl_core_report_facilities WHERE Resolution NOT IN ('NeedsUpdate','AlreadyCorrect')) q;
    IF NOT v_apply THEN
      SELECT 'CaseManager' EntityType,Resolution,COUNT(*) RowCount FROM tmp_sl_core_report_case_managers GROUP BY Resolution UNION ALL SELECT 'LawFirmDetails',Resolution,COUNT(*) FROM tmp_sl_core_report_law_firms GROUP BY Resolution UNION ALL SELECT 'MedicalFacility',Resolution,COUNT(*) FROM tmp_sl_core_report_facilities GROUP BY Resolution;
      SELECT v_changes ChangesToApply,v_conflicts Conflicts;
    ELSE
      IF p_expected_changes<>v_changes THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTRPF-006 expected change count does not match dry run'; END IF;
      IF v_conflicts<>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='LSLTRPF-007 report-party/facility backfill has conflicts; no rows were changed'; END IF;
      START TRANSACTION; SET v_in_transaction=TRUE;
      UPDATE liens_Cases c JOIN tmp_sl_core_report_case_managers s ON s.TargetCaseId=c.Id AND s.Resolution='NeedsUpdate' SET c.Notes=CASE WHEN s.ExistingManagerId IS NOT NULL THEN c.Notes WHEN c.Notes IS NULL OR TRIM(c.Notes)='' THEN CONCAT('[legacy-meta]',CHAR(10),'caseManagerId=',s.TargetContactId) WHEN LOCATE('[legacy-meta]',c.Notes)=0 THEN CONCAT(c.Notes,CHAR(10),CHAR(10),'[legacy-meta]',CHAR(10),'caseManagerId=',s.TargetContactId) ELSE CONCAT(c.Notes,'; caseManagerId=',s.TargetContactId) END,c.UpdatedAtUtc=UTC_TIMESTAMP(6),c.UpdatedByUserId=v_user_id;
      SET v_updated=ROW_COUNT();
      UPDATE liens_Contacts c JOIN tmp_sl_core_report_case_managers s ON s.TargetContactId=c.Id AND s.Resolution='NeedsUpdate' SET c.Email=COALESCE(NULLIF(TRIM(c.Email),''),s.SourceEmail),c.UpdatedAtUtc=UTC_TIMESTAMP(6),c.UpdatedByUserId=v_user_id;
      UPDATE liens_Contacts c JOIN tmp_sl_core_report_law_firms s ON s.TargetContactId=c.Id AND s.Resolution='NeedsUpdate' SET c.Phone=COALESCE(NULLIF(TRIM(c.Phone),''),s.SourcePhone),c.AddressLine1=COALESCE(NULLIF(TRIM(c.AddressLine1),''),s.SourceAddress),c.City=COALESCE(NULLIF(TRIM(c.City),''),s.SourceCity),c.State=COALESCE(NULLIF(TRIM(c.State),''),s.SourceState),c.PostalCode=COALESCE(NULLIF(TRIM(c.PostalCode),''),s.SourceZip),c.UpdatedAtUtc=UTC_TIMESTAMP(6),c.UpdatedByUserId=v_user_id;
      UPDATE liens_Liens l JOIN tmp_sl_core_report_facilities s ON s.TargetLienId=l.Id AND s.Resolution='NeedsUpdate' SET l.FacilityId=COALESCE(l.FacilityId,s.TargetFacilityId),l.UpdatedAtUtc=UTC_TIMESTAMP(6),l.UpdatedByUserId=v_user_id;
      UPDATE liens_Facilities f JOIN tmp_sl_core_report_facilities s ON s.TargetFacilityId=f.Id AND s.Resolution='NeedsUpdate' SET f.AddressLine1=COALESCE(NULLIF(TRIM(f.AddressLine1),''),s.SourceAddress),f.City=COALESCE(NULLIF(TRIM(f.City),''),s.SourceCity),f.State=COALESCE(NULLIF(TRIM(f.State),''),s.SourceState),f.PostalCode=COALESCE(NULLIF(TRIM(f.PostalCode),''),s.SourceZip),f.UpdatedAtUtc=UTC_TIMESTAMP(6),f.UpdatedByUserId=v_user_id;
      COMMIT; SET v_in_transaction=FALSE; SELECT v_changes RowsUpdated,v_changes ExpectedRowsUpdated;
    END IF;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_report_case_managers; DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_report_law_firms; DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_report_facilities; DO RELEASE_LOCK(v_lock_name);
END$$
DELIMITER ;
