-- Litigation case-status repair from Litigation Status.xlsx
--
-- The supplied workbook has 1,136 lien-level rows for 377 unique cases. Every
-- target case currently has Status = InNegotiation. This repair changes the
-- physical liens_Cases.Status value to the reviewed attachment statuses:
--   * Litigation (Open): 166 cases
--   * Litigation (Pending): 211 cases
--
-- Run the complete file in DBeaver using Execute SQL Script. Start with
-- @apply = 0, review the result, then copy ChangesToApply into
-- @expected_case_updates, set @apply = 1, and rerun the entire script.
-- No rows are updated unless every safety check passes.
--
-- This script never changes lien lifecycle statuses. The workbook's "Lien ID"
-- column identifies the affected liens, but its field is case_status, so the
-- correction belongs on the linked liens_Cases record. It does not alter Notes
-- or legacy metadata.

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Required: replace both placeholders before execution.
SET @tenant_id = '<tenant-guid>';
SET @actor_user_id = '<identity-user-guid>';

-- Dry run settings. Leave these unchanged for the first run.
SET @apply = 0;
SET @expected_case_updates = -1;

SET @target_schema = DATABASE();
SET @tenant_id = LOWER(TRIM(@tenant_id));
SET @actor_user_id = LOWER(TRIM(@actor_user_id));
SET @apply = IF(@apply = 1, 1, 0);

DROP TEMPORARY TABLE IF EXISTS tmp_litigation_status_input;
CREATE TEMPORARY TABLE tmp_litigation_status_input (
    CaseNumber VARCHAR(50) NOT NULL,
    DesiredStatus VARCHAR(50) NOT NULL,
    PRIMARY KEY (CaseNumber)
) ENGINE=InnoDB;

INSERT INTO tmp_litigation_status_input (CaseNumber, DesiredStatus) VALUES
    ('22-00024', 'Litigation (Pending)'),
    ('22-00032', 'Litigation (Pending)'),
    ('22-00038', 'Litigation (Pending)'),
    ('22-00049', 'Litigation (Pending)'),
    ('22-00054', 'Litigation (Pending)'),
    ('22-00081', 'Litigation (Open)'),
    ('22-00199', 'Litigation (Open)'),
    ('22-00249', 'Litigation (Pending)'),
    ('22-00250', 'Litigation (Open)'),
    ('22-00335', 'Litigation (Pending)'),
    ('22-00355', 'Litigation (Pending)'),
    ('22-00370', 'Litigation (Pending)'),
    ('22-00429', 'Litigation (Pending)'),
    ('23-00088', 'Litigation (Open)'),
    ('23-00107', 'Litigation (Pending)'),
    ('23-00115', 'Litigation (Pending)'),
    ('23-00155', 'Litigation (Pending)'),
    ('23-00165', 'Litigation (Pending)'),
    ('23-00194', 'Litigation (Pending)'),
    ('23-00218', 'Litigation (Pending)'),
    ('23-00237', 'Litigation (Pending)'),
    ('23-00242', 'Litigation (Pending)'),
    ('23-00268', 'Litigation (Pending)'),
    ('23-00280', 'Litigation (Pending)'),
    ('23-00288', 'Litigation (Open)'),
    ('23-00289', 'Litigation (Open)'),
    ('23-00290', 'Litigation (Pending)'),
    ('23-00292', 'Litigation (Pending)'),
    ('23-00298', 'Litigation (Open)'),
    ('23-00345', 'Litigation (Pending)'),
    ('23-00357', 'Litigation (Pending)'),
    ('23-00367', 'Litigation (Pending)'),
    ('23-00384', 'Litigation (Open)'),
    ('23-00412', 'Litigation (Open)'),
    ('23-00451', 'Litigation (Pending)'),
    ('23-00461', 'Litigation (Pending)'),
    ('23-00492', 'Litigation (Pending)'),
    ('23-00502', 'Litigation (Pending)'),
    ('23-00521', 'Litigation (Open)'),
    ('23-00524', 'Litigation (Pending)'),
    ('23-00535', 'Litigation (Open)'),
    ('23-00550', 'Litigation (Pending)'),
    ('23-00556', 'Litigation (Pending)'),
    ('23-00557', 'Litigation (Pending)'),
    ('23-00565', 'Litigation (Open)'),
    ('23-00586', 'Litigation (Pending)'),
    ('23-00587', 'Litigation (Open)'),
    ('23-00647', 'Litigation (Pending)'),
    ('23-00658', 'Litigation (Pending)'),
    ('23-00675', 'Litigation (Pending)'),
    ('23-00683', 'Litigation (Pending)'),
    ('23-00697', 'Litigation (Pending)'),
    ('23-00700', 'Litigation (Pending)'),
    ('23-00712', 'Litigation (Pending)'),
    ('23-00727', 'Litigation (Open)'),
    ('23-00732', 'Litigation (Pending)'),
    ('23-00761', 'Litigation (Open)'),
    ('24-00039', 'Litigation (Pending)'),
    ('24-00059', 'Litigation (Pending)'),
    ('24-00069', 'Litigation (Pending)'),
    ('24-00071', 'Litigation (Pending)'),
    ('24-00082', 'Litigation (Open)'),
    ('24-00110', 'Litigation (Pending)'),
    ('24-00134', 'Litigation (Pending)'),
    ('24-00191', 'Litigation (Pending)'),
    ('24-00197', 'Litigation (Open)'),
    ('24-00221', 'Litigation (Pending)'),
    ('24-00243', 'Litigation (Pending)'),
    ('24-00253', 'Litigation (Pending)'),
    ('24-00257', 'Litigation (Pending)'),
    ('24-00265', 'Litigation (Pending)'),
    ('24-00277', 'Litigation (Pending)'),
    ('24-00289', 'Litigation (Pending)'),
    ('24-00309', 'Litigation (Open)'),
    ('24-00315', 'Litigation (Open)'),
    ('24-00325', 'Litigation (Pending)'),
    ('24-00333', 'Litigation (Pending)'),
    ('24-00363', 'Litigation (Open)'),
    ('24-00388', 'Litigation (Pending)'),
    ('24-00398', 'Litigation (Pending)'),
    ('24-00400', 'Litigation (Open)'),
    ('24-00401', 'Litigation (Open)'),
    ('24-00436', 'Litigation (Pending)'),
    ('24-00452', 'Litigation (Pending)'),
    ('24-00462', 'Litigation (Open)'),
    ('24-00469', 'Litigation (Pending)'),
    ('24-00478', 'Litigation (Pending)'),
    ('24-00486', 'Litigation (Pending)'),
    ('24-00489', 'Litigation (Pending)'),
    ('24-00491', 'Litigation (Open)'),
    ('24-00497', 'Litigation (Open)'),
    ('24-00510', 'Litigation (Pending)'),
    ('24-00526', 'Litigation (Pending)'),
    ('24-00530', 'Litigation (Open)'),
    ('24-00535', 'Litigation (Pending)'),
    ('24-00537', 'Litigation (Pending)'),
    ('24-00551', 'Litigation (Open)'),
    ('24-00609', 'Litigation (Pending)'),
    ('24-00623', 'Litigation (Pending)'),
    ('24-00630', 'Litigation (Pending)'),
    ('24-00640', 'Litigation (Pending)'),
    ('24-00642', 'Litigation (Open)'),
    ('24-00674', 'Litigation (Pending)'),
    ('24-00703', 'Litigation (Open)'),
    ('24-00734', 'Litigation (Pending)'),
    ('24-00764', 'Litigation (Pending)'),
    ('24-00767', 'Litigation (Pending)'),
    ('24-00769', 'Litigation (Pending)'),
    ('24-00777', 'Litigation (Pending)'),
    ('24-00782', 'Litigation (Open)'),
    ('24-00788', 'Litigation (Open)'),
    ('24-00808', 'Litigation (Pending)'),
    ('24-00815', 'Litigation (Pending)'),
    ('24-00818', 'Litigation (Open)'),
    ('24-00821', 'Litigation (Pending)'),
    ('24-00822', 'Litigation (Pending)'),
    ('24-00858', 'Litigation (Pending)'),
    ('24-00860', 'Litigation (Pending)'),
    ('24-00869', 'Litigation (Pending)'),
    ('24-00872', 'Litigation (Pending)'),
    ('24-00873', 'Litigation (Pending)'),
    ('24-00874', 'Litigation (Pending)'),
    ('24-00876', 'Litigation (Pending)'),
    ('24-00880', 'Litigation (Pending)'),
    ('24-00917', 'Litigation (Open)'),
    ('24-00932', 'Litigation (Open)'),
    ('24-00943', 'Litigation (Pending)'),
    ('24-00955', 'Litigation (Pending)'),
    ('24-00969', 'Litigation (Open)'),
    ('24-00973', 'Litigation (Open)'),
    ('24-00975', 'Litigation (Open)'),
    ('24-00976', 'Litigation (Open)'),
    ('24-01002', 'Litigation (Pending)'),
    ('24-01019', 'Litigation (Pending)'),
    ('24-01020', 'Litigation (Pending)'),
    ('24-01033', 'Litigation (Open)'),
    ('24-01038', 'Litigation (Pending)'),
    ('24-01041', 'Litigation (Pending)'),
    ('24-01043', 'Litigation (Open)'),
    ('24-01089', 'Litigation (Pending)'),
    ('24-01103', 'Litigation (Pending)'),
    ('24-01104', 'Litigation (Pending)'),
    ('24-01114', 'Litigation (Open)'),
    ('24-01137', 'Litigation (Open)'),
    ('24-01181', 'Litigation (Open)'),
    ('24-01188', 'Litigation (Open)'),
    ('24-01191', 'Litigation (Pending)'),
    ('24-01225', 'Litigation (Pending)'),
    ('24-01226', 'Litigation (Pending)'),
    ('24-01270', 'Litigation (Open)'),
    ('24-01301', 'Litigation (Open)'),
    ('24-01313', 'Litigation (Pending)'),
    ('24-01327', 'Litigation (Pending)'),
    ('24-01369', 'Litigation (Pending)'),
    ('24-01375', 'Litigation (Open)'),
    ('24-01378', 'Litigation (Pending)'),
    ('24-01380', 'Litigation (Pending)'),
    ('24-01388', 'Litigation (Open)'),
    ('24-01404', 'Litigation (Open)'),
    ('24-01408', 'Litigation (Open)'),
    ('24-01409', 'Litigation (Open)'),
    ('24-01411', 'Litigation (Open)'),
    ('24-01414', 'Litigation (Pending)'),
    ('24-01415', 'Litigation (Open)'),
    ('24-01416', 'Litigation (Pending)'),
    ('24-01430', 'Litigation (Open)'),
    ('24-01434', 'Litigation (Pending)'),
    ('24-01436', 'Litigation (Pending)'),
    ('24-01462', 'Litigation (Pending)'),
    ('24-01463', 'Litigation (Pending)'),
    ('24-01492', 'Litigation (Pending)'),
    ('24-01518', 'Litigation (Pending)'),
    ('24-01522', 'Litigation (Pending)'),
    ('24-01523', 'Litigation (Pending)'),
    ('24-01524', 'Litigation (Pending)'),
    ('24-01532', 'Litigation (Pending)'),
    ('24-01540', 'Litigation (Open)'),
    ('24-01542', 'Litigation (Open)'),
    ('24-01599', 'Litigation (Pending)'),
    ('24-01610', 'Litigation (Open)'),
    ('24-01617', 'Litigation (Open)'),
    ('24-01621', 'Litigation (Pending)'),
    ('24-01622', 'Litigation (Pending)'),
    ('24-01624', 'Litigation (Open)'),
    ('24-01655', 'Litigation (Pending)'),
    ('24-01657', 'Litigation (Pending)'),
    ('24-01688', 'Litigation (Pending)'),
    ('24-01689', 'Litigation (Pending)'),
    ('24-01717', 'Litigation (Pending)'),
    ('24-01720', 'Litigation (Open)'),
    ('24-01721', 'Litigation (Open)'),
    ('24-01723', 'Litigation (Open)'),
    ('24-01726', 'Litigation (Open)'),
    ('24-01735', 'Litigation (Open)'),
    ('24-01756', 'Litigation (Pending)'),
    ('24-01809', 'Litigation (Pending)'),
    ('24-01810', 'Litigation (Pending)'),
    ('24-01838', 'Litigation (Pending)'),
    ('24-01874', 'Litigation (Open)'),
    ('24-01876', 'Litigation (Pending)'),
    ('24-01882', 'Litigation (Pending)'),
    ('24-01884', 'Litigation (Pending)'),
    ('24-01893', 'Litigation (Pending)'),
    ('24-01894', 'Litigation (Pending)'),
    ('24-01913', 'Litigation (Open)'),
    ('24-01945', 'Litigation (Open)'),
    ('24-01977', 'Litigation (Pending)'),
    ('24-01991', 'Litigation (Open)'),
    ('24-01992', 'Litigation (Pending)'),
    ('24-01999', 'Litigation (Pending)'),
    ('24-02007', 'Litigation (Open)'),
    ('24-02014', 'Litigation (Open)'),
    ('24-02030', 'Litigation (Pending)'),
    ('24-02038', 'Litigation (Open)'),
    ('24-02093', 'Litigation (Pending)'),
    ('25-00015', 'Litigation (Pending)'),
    ('25-00016', 'Litigation (Pending)'),
    ('25-00030', 'Litigation (Open)'),
    ('25-00046', 'Litigation (Open)'),
    ('25-00050', 'Litigation (Pending)'),
    ('25-00058', 'Litigation (Pending)'),
    ('25-00060', 'Litigation (Pending)'),
    ('25-00062', 'Litigation (Open)'),
    ('25-00074', 'Litigation (Pending)'),
    ('25-00107', 'Litigation (Pending)'),
    ('25-00115', 'Litigation (Pending)'),
    ('25-00137', 'Litigation (Open)'),
    ('25-00147', 'Litigation (Open)'),
    ('25-00149', 'Litigation (Pending)'),
    ('25-00198', 'Litigation (Open)'),
    ('25-00266', 'Litigation (Open)'),
    ('25-00284', 'Litigation (Pending)'),
    ('25-00314', 'Litigation (Open)'),
    ('25-00333', 'Litigation (Pending)'),
    ('25-00350', 'Litigation (Open)'),
    ('25-00376', 'Litigation (Open)'),
    ('25-00377', 'Litigation (Pending)'),
    ('25-00381', 'Litigation (Open)'),
    ('25-00388', 'Litigation (Pending)'),
    ('25-00391', 'Litigation (Open)'),
    ('25-00397', 'Litigation (Open)'),
    ('25-00398', 'Litigation (Pending)'),
    ('25-00403', 'Litigation (Open)'),
    ('25-00406', 'Litigation (Pending)'),
    ('25-00411', 'Litigation (Open)'),
    ('25-00452', 'Litigation (Open)'),
    ('25-00463', 'Litigation (Open)'),
    ('25-00535', 'Litigation (Pending)'),
    ('25-00536', 'Litigation (Open)'),
    ('25-00537', 'Litigation (Open)'),
    ('25-00539', 'Litigation (Open)'),
    ('25-00609', 'Litigation (Pending)'),
    ('25-00613', 'Litigation (Pending)'),
    ('25-00622', 'Litigation (Pending)'),
    ('25-00630', 'Litigation (Pending)'),
    ('25-00644', 'Litigation (Open)'),
    ('25-00649', 'Litigation (Pending)'),
    ('25-00660', 'Litigation (Pending)'),
    ('25-00697', 'Litigation (Open)'),
    ('25-00705', 'Litigation (Pending)'),
    ('25-00708', 'Litigation (Open)'),
    ('25-00709', 'Litigation (Open)'),
    ('25-00710', 'Litigation (Open)'),
    ('25-00714', 'Litigation (Open)'),
    ('25-00736', 'Litigation (Open)'),
    ('25-00738', 'Litigation (Open)'),
    ('25-00745', 'Litigation (Open)'),
    ('25-00772', 'Litigation (Open)'),
    ('25-00779', 'Litigation (Pending)'),
    ('25-00830', 'Litigation (Pending)'),
    ('25-00869', 'Litigation (Open)'),
    ('25-00886', 'Litigation (Pending)'),
    ('25-00919', 'Litigation (Open)'),
    ('25-00988', 'Litigation (Open)'),
    ('25-01029', 'Litigation (Pending)'),
    ('25-01068', 'Litigation (Pending)'),
    ('25-01071', 'Litigation (Pending)'),
    ('25-01089', 'Litigation (Pending)'),
    ('25-01116', 'Litigation (Pending)'),
    ('25-01208', 'Litigation (Open)'),
    ('25-01276', 'Litigation (Open)'),
    ('25-01307', 'Litigation (Pending)'),
    ('25-01311', 'Litigation (Open)'),
    ('25-01313', 'Litigation (Open)'),
    ('25-01397', 'Litigation (Pending)'),
    ('25-01398', 'Litigation (Pending)'),
    ('25-01408', 'Litigation (Pending)'),
    ('25-01420', 'Litigation (Open)'),
    ('25-01497', 'Litigation (Open)'),
    ('25-01498', 'Litigation (Open)'),
    ('25-01544', 'Litigation (Pending)'),
    ('25-01547', 'Litigation (Open)'),
    ('25-01552', 'Litigation (Open)'),
    ('25-01563', 'Litigation (Open)'),
    ('25-01573', 'Litigation (Open)'),
    ('25-01575', 'Litigation (Open)'),
    ('25-01610', 'Litigation (Open)'),
    ('25-01681', 'Litigation (Open)'),
    ('25-01740', 'Litigation (Pending)'),
    ('25-01764', 'Litigation (Pending)'),
    ('25-01819', 'Litigation (Pending)'),
    ('25-01881', 'Litigation (Open)'),
    ('25-01935', 'Litigation (Open)'),
    ('25-01939', 'Litigation (Open)'),
    ('25-01940', 'Litigation (Open)'),
    ('25-01943', 'Litigation (Pending)'),
    ('25-01950', 'Litigation (Open)'),
    ('25-01970', 'Litigation (Pending)'),
    ('25-01971', 'Litigation (Pending)'),
    ('25-01973', 'Litigation (Open)'),
    ('25-01975', 'Litigation (Pending)'),
    ('25-01988', 'Litigation (Open)'),
    ('25-02004', 'Litigation (Open)'),
    ('25-02014', 'Litigation (Open)'),
    ('25-02125', 'Litigation (Pending)'),
    ('25-02206', 'Litigation (Pending)'),
    ('25-02208', 'Litigation (Pending)'),
    ('25-02209', 'Litigation (Pending)'),
    ('25-02217', 'Litigation (Open)'),
    ('25-02253', 'Litigation (Open)'),
    ('25-02254', 'Litigation (Open)'),
    ('25-02259', 'Litigation (Open)'),
    ('25-02261', 'Litigation (Pending)'),
    ('25-02269', 'Litigation (Pending)'),
    ('25-02274', 'Litigation (Pending)'),
    ('25-02290', 'Litigation (Open)'),
    ('25-02342', 'Litigation (Open)'),
    ('25-02449', 'Litigation (Open)'),
    ('25-02459', 'Litigation (Pending)'),
    ('25-02483', 'Litigation (Pending)'),
    ('25-02485', 'Litigation (Pending)'),
    ('25-02504', 'Litigation (Pending)'),
    ('25-02574', 'Litigation (Open)'),
    ('25-02599', 'Litigation (Pending)'),
    ('25-02667', 'Litigation (Pending)'),
    ('25-02700', 'Litigation (Pending)'),
    ('25-02762', 'Litigation (Open)'),
    ('25-02812', 'Litigation (Open)'),
    ('25-02886', 'Litigation (Pending)'),
    ('25-02948', 'Litigation (Pending)'),
    ('25-02974', 'Litigation (Pending)'),
    ('25-03087', 'Litigation (Pending)'),
    ('25-03267', 'Litigation (Open)'),
    ('25-03301', 'Litigation (Pending)'),
    ('26-00214', 'Litigation (Open)'),
    ('26-00346', 'Litigation (Pending)'),
    ('26-00743', 'Litigation (Pending)'),
    ('26-00766', 'Litigation (Open)'),
    ('26-00782', 'Litigation (Open)'),
    ('26-00827', 'Litigation (Open)'),
    ('26-00994', 'Litigation (Pending)'),
    ('26-01126', 'Litigation (Open)'),
    ('26-01232', 'Litigation (Pending)'),
    ('26-01267', 'Litigation (Pending)'),
    ('26-31919', 'Litigation (Open)'),
    ('26-32012', 'Litigation (Open)'),
    ('26-32569', 'Litigation (Open)'),
    ('26-32570', 'Litigation (Open)'),
    ('26-32572', 'Litigation (Open)'),
    ('26-32578', 'Litigation (Open)'),
    ('26-32579', 'Litigation (Open)'),
    ('26-32581', 'Litigation (Open)'),
    ('26-32582', 'Litigation (Open)'),
    ('26-32583', 'Litigation (Open)'),
    ('26-32584', 'Litigation (Open)'),
    ('26-32585', 'Litigation (Open)'),
    ('26-32588', 'Litigation (Open)'),
    ('26-32591', 'Litigation (Open)'),
    ('26-32595', 'Litigation (Open)'),
    ('26-32596', 'Litigation (Open)'),
    ('26-32598', 'Litigation (Open)'),
    ('26-32601', 'Litigation (Open)'),
    ('26-32603', 'Litigation (Open)'),
    ('26-32604', 'Litigation (Open)'),
    ('26-32628', 'Litigation (Open)'),
    ('26-32632', 'Litigation (Open)'),
    ('26-33034', 'Litigation (Open)');

SELECT COUNT(*) INTO @input_case_count
FROM tmp_litigation_status_input;

SELECT COUNT(*) INTO @open_case_count
FROM tmp_litigation_status_input
WHERE DesiredStatus = 'Litigation (Open)';

SELECT COUNT(*) INTO @pending_case_count
FROM tmp_litigation_status_input
WHERE DesiredStatus = 'Litigation (Pending)';

SELECT COUNT(*) INTO @target_table_count
FROM information_schema.tables
WHERE table_schema = @target_schema
  AND table_type = 'BASE TABLE'
  AND table_name = 'liens_Cases';

SELECT COUNT(*) INTO @target_column_count
FROM information_schema.columns
WHERE table_schema = @target_schema
  AND table_name = 'liens_Cases'
  AND column_name IN (
      'Id', 'TenantId', 'CaseNumber', 'Status',
      'UpdatedAtUtc', 'UpdatedByUserId'
  );

SET @preflight_ok =
    @target_schema IN ('LS_QA_LIENS', 'LS_LIENS')
    AND CHAR_LENGTH(@tenant_id) = 36
    AND CHAR_LENGTH(@actor_user_id) = 36
    AND SUBSTRING(@tenant_id, 9, 1) = '-'
    AND SUBSTRING(@tenant_id, 14, 1) = '-'
    AND SUBSTRING(@tenant_id, 19, 1) = '-'
    AND SUBSTRING(@tenant_id, 24, 1) = '-'
    AND SUBSTRING(@actor_user_id, 9, 1) = '-'
    AND SUBSTRING(@actor_user_id, 14, 1) = '-'
    AND SUBSTRING(@actor_user_id, 19, 1) = '-'
    AND SUBSTRING(@actor_user_id, 24, 1) = '-'
    AND UNHEX(REPLACE(@tenant_id, '-', '')) IS NOT NULL
    AND UNHEX(REPLACE(@actor_user_id, '-', '')) IS NOT NULL
    AND @input_case_count = 377
    AND @open_case_count = 166
    AND @pending_case_count = 211
    AND @target_table_count = 1
    AND @target_column_count = 6;

DROP TEMPORARY TABLE IF EXISTS tmp_litigation_status_plan;
CREATE TEMPORARY TABLE tmp_litigation_status_plan AS
SELECT
    input.CaseNumber,
    input.DesiredStatus,
    target.Id AS CaseId,
    target.TenantId AS TargetTenantId,
    target.Status AS TargetStatusBefore,
    NULL AS BlockingReason,
    0 AS NeedsUpdate
FROM tmp_litigation_status_input input
LEFT JOIN liens_Cases target
  ON target.TenantId = @tenant_id
 AND target.CaseNumber = input.CaseNumber;

ALTER TABLE tmp_litigation_status_plan
    ADD PRIMARY KEY (CaseNumber),
    ADD KEY IX_tmp_litigation_status_plan_CaseId (CaseId);

UPDATE tmp_litigation_status_plan
SET BlockingReason = CASE
        WHEN CaseId IS NULL THEN 'MissingTargetCase'
        WHEN TargetTenantId <> @tenant_id THEN 'InvalidTargetTenant'
        WHEN TargetStatusBefore <> 'InNegotiation' THEN 'UnexpectedCaseStatusPreimage'
        WHEN DesiredStatus NOT IN ('Litigation (Open)', 'Litigation (Pending)')
            THEN 'InvalidDesiredStatus'
        ELSE NULL
    END,
    NeedsUpdate = CASE
        WHEN CaseId IS NOT NULL
         AND TargetTenantId = @tenant_id
         AND TargetStatusBefore = 'InNegotiation'
         AND DesiredStatus IN ('Litigation (Open)', 'Litigation (Pending)')
            THEN 1
        ELSE 0
    END;

SELECT COUNT(*) INTO @blocking_case_count
FROM tmp_litigation_status_plan
WHERE BlockingReason IS NOT NULL;

SELECT COUNT(*) INTO @changes_to_apply
FROM tmp_litigation_status_plan
WHERE BlockingReason IS NULL
  AND NeedsUpdate = 1;

SELECT COUNT(*) INTO @already_correct_count
FROM tmp_litigation_status_plan
WHERE BlockingReason IS NULL
  AND NeedsUpdate = 0;

SET @apply_permitted =
    @apply = 1
    AND @preflight_ok = 1
    AND @blocking_case_count = 0
    AND @expected_case_updates = @changes_to_apply;

SELECT
    @target_schema AS TargetSchema,
    @tenant_id AS TenantId,
    @preflight_ok AS PreflightPassed,
    @apply AS ApplyRequested,
    @apply_permitted AS ApplyPermitted,
    @input_case_count AS WorkbookCases,
    @open_case_count AS LitigationOpenCases,
    @pending_case_count AS LitigationPendingCases,
    @changes_to_apply AS ChangesToApply,
    @already_correct_count AS AlreadyCorrect,
    @blocking_case_count AS BlockingCases;

SELECT BlockingReason, COUNT(*) AS AffectedCases
FROM tmp_litigation_status_plan
WHERE BlockingReason IS NOT NULL
GROUP BY BlockingReason
ORDER BY BlockingReason;

SELECT CaseNumber, DesiredStatus, TargetStatusBefore, BlockingReason
FROM tmp_litigation_status_plan
WHERE BlockingReason IS NOT NULL
ORDER BY CaseNumber
LIMIT 100;

START TRANSACTION;

SELECT COUNT(*) INTO @preimage_matches
FROM liens_Cases target
INNER JOIN tmp_litigation_status_plan plan
  ON plan.CaseId = target.Id
WHERE @apply_permitted = 1
  AND plan.BlockingReason IS NULL
  AND plan.NeedsUpdate = 1
  AND target.TenantId = @tenant_id
  AND target.Status = 'InNegotiation'
FOR UPDATE;

SET @apply_permitted =
    @apply_permitted = 1
    AND @preimage_matches = @changes_to_apply;

UPDATE liens_Cases target
INNER JOIN tmp_litigation_status_plan plan
  ON plan.CaseId = target.Id
SET target.Status = plan.DesiredStatus,
    target.UpdatedAtUtc = UTC_TIMESTAMP(6),
    target.UpdatedByUserId = @actor_user_id
WHERE @apply_permitted = 1
  AND plan.BlockingReason IS NULL
  AND plan.NeedsUpdate = 1
  AND target.TenantId = @tenant_id
  AND target.Status = 'InNegotiation';

SET @rows_updated = ROW_COUNT();

SELECT COUNT(*) INTO @postcondition_errors
FROM liens_Cases target
INNER JOIN tmp_litigation_status_plan plan
  ON plan.CaseId = target.Id
WHERE @apply_permitted = 1
  AND plan.BlockingReason IS NULL
  AND plan.NeedsUpdate = 1
  AND (
      target.TenantId <> @tenant_id
      OR target.Status <> plan.DesiredStatus
  );

SET @apply_permitted =
    @apply_permitted = 1
    AND @rows_updated = @changes_to_apply
    AND @postcondition_errors = 0;

-- If a preimage or postcondition check failed, undo the attempted update.
-- DBeaver still receives the result row below showing ApplyPermitted = 0.
SET @transaction_end_sql = IF(@apply_permitted = 1, 'COMMIT', 'ROLLBACK');
PREPARE litigation_status_transaction_end FROM @transaction_end_sql;
EXECUTE litigation_status_transaction_end;
DEALLOCATE PREPARE litigation_status_transaction_end;

SELECT
    @apply_permitted AS ApplyPermitted,
    @changes_to_apply AS ExpectedCaseUpdates,
    @preimage_matches AS LockedPreimages,
    @rows_updated AS RowsUpdated,
    @postcondition_errors AS PostconditionErrors,
    CASE
        WHEN @apply = 0 THEN 'Dry run complete: no changes were written.'
        WHEN @preflight_ok <> 1 THEN 'No changes written: preflight failed.'
        WHEN @blocking_case_count <> 0 THEN 'No changes written: resolve blocking cases.'
        WHEN @expected_case_updates <> @changes_to_apply THEN 'No changes written: expected count does not match dry run.'
        WHEN @preimage_matches <> @changes_to_apply THEN 'No changes written: target cases changed after preflight.'
        WHEN @rows_updated <> @changes_to_apply OR @postcondition_errors <> 0
            THEN 'Review required: transaction completed but postcondition validation failed.'
        ELSE 'Applied successfully.'
    END AS Result;
