-- SL-CORE case information for V3_Reduction_Fix_List_192.xlsx.
--
-- Run this script in DBeaver/MySQL on the server that hosts the source schema
-- under the exact name `SL-CORE`. It does not modify persistent data; the only
-- write is to a connection-local temporary table that is dropped at the end.
--
-- Result set 1: reconciliation summary.
-- Result set 2: missing, duplicate, deleted, or case-code-mismatch exceptions.
-- Result set 3: every requested workbook lien, in workbook order, with the
--               complete raw SL_CASE and SL_LEINS_MEDICAL source records.
--               Duplicate source lien codes expand to more than one row.
-- The requested reduction date is emitted as 2026-04-27 for every detail row.
-- It is an output value only; this script does not change immutable SL-CORE.

SET @requested_reduction_date = DATE('2026-04-27');

DROP TEMPORARY TABLE IF EXISTS tmp_v3_reduction_fix_list;

CREATE TEMPORARY TABLE tmp_v3_reduction_fix_list (
    WorkbookRow INT NOT NULL,
    LienCode VARCHAR(50) NOT NULL,
    ExpectedCaseCode VARCHAR(50) NOT NULL,
    PRIMARY KEY (WorkbookRow),
    UNIQUE KEY UX_tmp_v3_reduction_fix_list_lien_code (LienCode)
);

INSERT INTO tmp_v3_reduction_fix_list (WorkbookRow, LienCode, ExpectedCaseCode)
VALUES
(1, '26-32489-01', '26-32489'),
(2, '26-31899-01', '26-31899'),
(3, '26-32185-03', '26-32185'),
(4, '26-32185-02', '26-32185'),
(5, '26-32185-01', '26-32185'),
(6, '26-33080-01', '26-33080'),
(7, '26-32046-02', '26-32046'),
(8, '26-32119-02', '26-32119'),
(9, '26-32119-03', '26-32119'),
(10, '25-02882-11', '25-02882'),
(11, '26-32293-02', '26-32293'),
(12, '26-32498-01', '26-32498'),
(13, '26-32293-03', '26-32293'),
(14, '26-31907-01', '26-31907'),
(15, '26-32130-01', '26-32130'),
(16, '26-32180-01', '26-32180'),
(17, '26-32214-01', '26-32214'),
(18, '26-32080-01', '26-32080'),
(19, '26-32929-01', '26-32929'),
(20, '25-01775-04', '25-01775'),
(21, '25-01775-03', '25-01775'),
(22, '25-01775-06', '25-01775'),
(23, '25-01775-05', '25-01775'),
(24, '26-32187-01', '26-32187'),
(25, '25-02882-12', '25-02882'),
(26, '26-32127-01', '26-32127'),
(27, '26-32216-01', '26-32216'),
(28, '25-01775-02', '25-01775'),
(29, '26-31918-01', '26-31918'),
(30, '26-01205-02', '26-01205'),
(31, '26-32037-02', '26-32037'),
(32, '26-32205-01', '26-32205'),
(33, '26-31986-01', '26-31986'),
(34, '26-32699-01', '26-32699'),
(35, '26-01236-02', '26-01236'),
(36, '26-32014-01', '26-32014'),
(37, '26-32046-01', '26-32046'),
(38, '26-32025-01', '26-32025'),
(39, '26-32150-01', '26-32150'),
(40, '26-32642-01', '26-32642'),
(41, '25-00067-06', '25-00067'),
(42, '26-32290-01', '26-32290'),
(43, '26-32007-02', '26-32007'),
(44, '26-32402-01', '26-32402'),
(45, '26-32321-01', '26-32321'),
(46, '26-00457-08', '26-00457'),
(47, '26-00457-07', '26-00457'),
(48, '26-00457-06', '26-00457'),
(49, '26-00457-05', '26-00457'),
(50, '26-32059-01', '26-32059'),
(51, '26-32155-01', '26-32155'),
(52, '26-31890-01', '26-31890'),
(53, '26-32002-01', '26-32002'),
(54, '26-32555-01', '26-32555'),
(55, '26-32255-01', '26-32255'),
(56, '26-32306-01', '26-32306'),
(57, '26-32289-01', '26-32289'),
(58, '26-31973-01', '26-31973'),
(59, '25-01775-01', '25-01775'),
(60, '26-32185-04', '26-32185'),
(61, '26-31880-02', '26-31880'),
(62, '26-32461-01', '26-32461'),
(63, '26-32407-01', '26-32407'),
(64, '26-32336-01', '26-32336'),
(65, '26-32254-01', '26-32254'),
(66, '26-32253-01', '26-32253'),
(67, '23-00547-02', '23-00547'),
(68, '26-32119-01', '26-32119'),
(69, '26-32114-01', '26-32114'),
(70, '26-32020-01', '26-32020'),
(71, '26-31931-01', '26-31931'),
(72, '26-31900-01', '26-31900'),
(73, '26-31882-01', '26-31882'),
(74, '26-32518-01', '26-32518'),
(75, '26-01011-11', '26-01011'),
(76, '26-32930-01', '26-32930'),
(77, '26-32761-01', '26-32761'),
(78, '26-32343-01', '26-32343'),
(79, '26-32296-01', '26-32296'),
(80, '26-32006-01', '26-32006'),
(81, '26-31989-01', '26-31989'),
(82, '26-31915-01', '26-31915'),
(83, '26-31875-01', '26-31875'),
(84, '26-31941-01', '26-31941'),
(85, '26-32439-01', '26-32439'),
(86, '26-32071-01', '26-32071'),
(87, '26-32070-01', '26-32070'),
(88, '26-32024-01', '26-32024'),
(89, '26-31944-01', '26-31944'),
(90, '26-32081-01', '26-32081'),
(91, '26-32339-02', '26-32339'),
(92, '26-32673-01', '26-32673'),
(93, '26-32008-01', '26-32008'),
(94, '26-31999-01', '26-31999'),
(95, '26-31983-01', '26-31983'),
(96, '26-32173-01', '26-32173'),
(97, '26-32037-01', '26-32037'),
(98, '26-31944-02', '26-31944'),
(99, '26-00283-02', '26-00283'),
(100, '26-32568-01', '26-32568'),
(101, '26-32457-01', '26-32457'),
(102, '26-32014-02', '26-32014'),
(103, '26-32381-01', '26-32381'),
(104, '26-32311-01', '26-32311'),
(105, '26-31985-02', '26-31985'),
(106, '26-31978-01', '26-31978'),
(107, '26-01125-04', '26-01125'),
(108, '26-32021-03', '26-32021'),
(109, '26-32782-01', '26-32782'),
(110, '26-32293-04', '26-32293'),
(111, '26-00457-09', '26-00457'),
(112, '26-31936-03', '26-31936'),
(113, '26-00950-02', '26-00950'),
(114, '26-32571-01', '26-32571'),
(115, '26-31936-02', '26-31936'),
(116, '26-32652-01', '26-32652'),
(117, '26-32358-01', '26-32358'),
(118, '26-31981-01', '26-31981'),
(119, '26-31954-02', '26-31954'),
(120, '26-31965-02', '26-31965'),
(121, '26-32544-01', '26-32544'),
(122, '26-32143-02', '26-32143'),
(123, '26-32463-01', '26-32463'),
(124, '26-32082-02', '26-32082'),
(125, '26-31900-02', '26-31900'),
(126, '26-32143-01', '26-32143'),
(127, '26-32104-01', '26-32104'),
(128, '26-01094-02', '26-01094'),
(129, '26-32959-01', '26-32959'),
(130, '26-31948-01', '26-31948'),
(131, '26-32377-03', '26-32377'),
(132, '26-32377-02', '26-32377'),
(133, '26-01197-03', '26-01197'),
(134, '26-32024-02', '26-32024'),
(135, '26-00673-02', '26-00673'),
(136, '26-32259-01', '26-32259'),
(137, '26-31954-01', '26-31954'),
(138, '26-31945-01', '26-31945'),
(139, '26-00358-04', '26-00358'),
(140, '26-31916-01', '26-31916'),
(141, '26-31951-01', '26-31951'),
(142, '25-02401-02', '25-02401'),
(143, '26-31994-01', '26-31994'),
(144, '26-32314-01', '26-32314'),
(145, '26-01239-02', '26-01239'),
(146, '26-32111-02', '26-32111'),
(147, '26-31886-01', '26-31886'),
(148, '26-00359-04', '26-00359'),
(149, '26-32319-01', '26-32319'),
(150, '26-32023-01', '26-32023'),
(151, '26-32149-01', '26-32149'),
(152, '26-31973-02', '26-31973'),
(153, '26-31985-01', '26-31985'),
(154, '26-31944-03', '26-31944'),
(155, '26-31936-06', '26-31936'),
(156, '26-31936-04', '26-31936'),
(157, '25-02882-10', '25-02882'),
(158, '26-32082-01', '26-32082'),
(159, '26-31968-01', '26-31968'),
(160, '26-32021-02', '26-32021'),
(161, '26-31945-02', '26-31945'),
(162, '26-32959-02', '26-32959'),
(163, '26-32007-01', '26-32007'),
(164, '26-00633-07', '26-00633'),
(165, '26-32339-01', '26-32339'),
(166, '26-32538-01', '26-32538'),
(167, '26-32377-01', '26-32377'),
(168, '26-32001-01', '26-32001'),
(169, '26-32082-03', '26-32082'),
(170, '26-31968-02', '26-31968'),
(171, '26-00388-07', '26-00388'),
(172, '26-00388-06', '26-00388'),
(173, '26-31936-01', '26-31936'),
(174, '26-01012-12', '26-01012'),
(175, '26-01012-11', '26-01012'),
(176, '26-31948-02', '26-31948'),
(177, '25-02732-04', '25-02732'),
(178, '26-32311-02', '26-32311'),
(179, '26-31880-01', '26-31880'),
(180, '26-32021-01', '26-32021'),
(181, '26-31978-02', '26-31978'),
(182, '26-31989-02', '26-31989'),
(183, '26-01011-13', '26-01011'),
(184, '26-01011-12', '26-01011'),
(185, '26-31934-01', '26-31934'),
(186, '26-32111-01', '26-32111'),
(187, '26-31965-01', '26-31965'),
(188, '26-00388-05', '26-00388'),
(189, '26-01197-02', '26-01197'),
(190, '26-32032-01', '26-32032'),
(191, '26-00388-08', '26-00388'),
(192, '26-32396-01', '26-32396');

-- Reconcile the workbook list to SL-CORE before using the detail result.
SELECT
    COUNT(*) AS RequestedLienCount,
    SUM(CASE WHEN matches.LienMatchCount = 0 THEN 1 ELSE 0 END) AS MissingLienCount,
    SUM(CASE WHEN matches.LienMatchCount = 1 THEN 1 ELSE 0 END) AS ExactlyOneLienMatchCount,
    SUM(CASE WHEN matches.LienMatchCount > 1 THEN 1 ELSE 0 END) AS DuplicateLienCodeCount,
    SUM(CASE WHEN matches.ActiveLienMatchCount = 1 THEN 1 ELSE 0 END) AS ActiveLienMatchCount,
    SUM(CASE WHEN matches.CaseMatchCount = 1 THEN 1 ELSE 0 END) AS CaseMatchCount,
    SUM(CASE WHEN matches.ExpectedCaseCodeMatchCount = 1 THEN 1 ELSE 0 END) AS ExpectedCaseCodeMatchCount,
    COUNT(DISTINCT matches.LegacyCaseId) AS DistinctMatchedCaseCount
FROM (
    SELECT
        requested.WorkbookRow,
        COUNT(DISTINCT lien.LM_ID) AS LienMatchCount,
        COUNT(DISTINCT CASE
            WHEN UPPER(TRIM(COALESCE(lien.LM_IS_DELETED, 'N'))) <> 'Y' THEN lien.LM_ID
        END) AS ActiveLienMatchCount,
        COUNT(DISTINCT source_case.CASE_ID) AS CaseMatchCount,
        COUNT(DISTINCT CASE
            WHEN TRIM(source_case.CASE_CODE) = requested.ExpectedCaseCode THEN source_case.CASE_ID
        END) AS ExpectedCaseCodeMatchCount,
        MIN(source_case.CASE_ID) AS LegacyCaseId
    FROM tmp_v3_reduction_fix_list requested
    LEFT JOIN `SL-CORE`.`SL_LEINS_MEDICAL` lien
      ON TRIM(lien.LM_CODE) = requested.LienCode
    LEFT JOIN `SL-CORE`.`SL_CASE` source_case
      ON source_case.CASE_ID = lien.LM_CASE_ID
    GROUP BY requested.WorkbookRow
) matches;

-- This result set should be empty. Any returned row needs reconciliation.
SELECT
    requested.WorkbookRow,
    requested.LienCode,
    requested.ExpectedCaseCode,
    COUNT(DISTINCT lien.LM_ID) AS LienMatchCount,
    GROUP_CONCAT(DISTINCT lien.LM_ID ORDER BY lien.LM_ID) AS MatchedLegacyLienIds,
    GROUP_CONCAT(DISTINCT source_case.CASE_ID ORDER BY source_case.CASE_ID) AS MatchedLegacyCaseIds,
    GROUP_CONCAT(DISTINCT TRIM(source_case.CASE_CODE)
                 ORDER BY TRIM(source_case.CASE_CODE)) AS MatchedCaseCodes,
    GROUP_CONCAT(DISTINCT COALESCE(lien.LM_IS_DELETED, 'N')
                 ORDER BY COALESCE(lien.LM_IS_DELETED, 'N')) AS LienDeletedFlags,
    GROUP_CONCAT(DISTINCT COALESCE(source_case.CASE_IS_DELETED, 'N')
                 ORDER BY COALESCE(source_case.CASE_IS_DELETED, 'N')) AS CaseDeletedFlags
FROM tmp_v3_reduction_fix_list requested
LEFT JOIN `SL-CORE`.`SL_LEINS_MEDICAL` lien
  ON TRIM(lien.LM_CODE) = requested.LienCode
LEFT JOIN `SL-CORE`.`SL_CASE` source_case
  ON source_case.CASE_ID = lien.LM_CASE_ID
GROUP BY requested.WorkbookRow, requested.LienCode, requested.ExpectedCaseCode
HAVING COUNT(DISTINCT lien.LM_ID) <> 1
    OR COUNT(DISTINCT source_case.CASE_ID) <> 1
    OR COUNT(DISTINCT CASE
        WHEN TRIM(source_case.CASE_CODE) = requested.ExpectedCaseCode THEN source_case.CASE_ID
    END) <> 1
    OR COUNT(DISTINCT CASE
        WHEN UPPER(TRIM(COALESCE(lien.LM_IS_DELETED, 'N'))) = 'Y' THEN lien.LM_ID
    END) <> 0
    OR COUNT(DISTINCT CASE
        WHEN UPPER(TRIM(COALESCE(source_case.CASE_IS_DELETED, 'N'))) = 'Y' THEN source_case.CASE_ID
    END) <> 0
ORDER BY requested.WorkbookRow;

-- Full case and lien source records. Keep the leading workbook columns when
-- exporting so each database row can be traced to the spreadsheet.
SELECT
    requested.WorkbookRow,
    requested.LienCode AS WorkbookLienCode,
    requested.ExpectedCaseCode AS WorkbookCaseCode,
    CASE
        WHEN lien.LM_ID IS NULL THEN 'LienNotFound'
        WHEN source_case.CASE_ID IS NULL THEN 'CaseNotFound'
        WHEN UPPER(TRIM(COALESCE(lien.LM_IS_DELETED, 'N'))) = 'Y' THEN 'LienDeleted'
        WHEN UPPER(TRIM(COALESCE(source_case.CASE_IS_DELETED, 'N'))) = 'Y' THEN 'CaseDeleted'
        WHEN TRIM(source_case.CASE_CODE) <> requested.ExpectedCaseCode THEN 'CaseCodeMismatch'
        ELSE 'Matched'
    END AS MatchStatus,
    @requested_reduction_date AS RequestedReductionDate,
    source_case.*,
    lien.*
FROM tmp_v3_reduction_fix_list requested
LEFT JOIN `SL-CORE`.`SL_LEINS_MEDICAL` lien
  ON TRIM(lien.LM_CODE) = requested.LienCode
LEFT JOIN `SL-CORE`.`SL_CASE` source_case
  ON source_case.CASE_ID = lien.LM_CASE_ID
ORDER BY requested.WorkbookRow, lien.LM_ID;

DROP TEMPORARY TABLE IF EXISTS tmp_v3_reduction_fix_list;
