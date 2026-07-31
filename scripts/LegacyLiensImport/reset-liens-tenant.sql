-- liens_reset_tenant
--
-- Removes all tenant-scoped Liens Core data for one tenant from the current
-- LS_LIENS or LS_QA_LIENS schema.  It deliberately does not touch identity,
-- source-system, or any non-liens_ schema.
--
-- Usage:
--   CALL liens_reset_tenant('<tenant-guid>', '0');  -- preflight only
--   CALL liens_reset_tenant('<tenant-guid>', '1');  -- irreversible reset
--
-- The procedure discovers every current liens_ base table with a TenantId
-- column.  It blocks if a non-tenant-scoped child table references any of
-- those tables, preventing an orphaned cross-scope record.
--
-- Error prefix: LSRT-

DROP PROCEDURE IF EXISTS liens_reset_tenant;

DELIMITER $$

CREATE PROCEDURE liens_reset_tenant(
    IN p_tenant_id CHAR(36),
    IN p_apply     CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id           CHAR(36);
    DECLARE v_apply               BOOLEAN;
    DECLARE v_table_name          VARCHAR(64);
    DECLARE v_table_count         INT DEFAULT 0;
    DECLARE v_unscoped_fk_count   INT DEFAULT 0;
    DECLARE v_remaining_rows      BIGINT DEFAULT 0;
    DECLARE v_original_fk_checks  INT DEFAULT 1;
    DECLARE v_fk_checks_changed   BOOLEAN DEFAULT FALSE;
    DECLARE v_in_transaction      BOOLEAN DEFAULT FALSE;
    DECLARE v_done                BOOLEAN DEFAULT FALSE;
    DECLARE v_cursor_open         BOOLEAN DEFAULT FALSE;

    DECLARE cur_reset_tables CURSOR FOR
        SELECT TableName FROM tmp_lstr_reset_tables ORDER BY TableName;

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = TRUE;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN
            ROLLBACK;
            SET v_in_transaction = FALSE;
        END IF;
        IF v_fk_checks_changed THEN
            SET @@session.foreign_key_checks = v_original_fk_checks;
        END IF;
        IF v_cursor_open THEN
            CLOSE cur_reset_tables;
            SET v_cursor_open = FALSE;
        END IF;
        SET @lstr_reset_sql = NULL;
        SET @lstr_reset_tenant = NULL;
        DROP TEMPORARY TABLE IF EXISTS tmp_lstr_reset_summary;
        DROP TEMPORARY TABLE IF EXISTS tmp_lstr_reset_child_tables;
        DROP TEMPORARY TABLE IF EXISTS tmp_lstr_reset_tables;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    IF v_tenant_id IS NULL
       OR CHAR_LENGTH(v_tenant_id) <> 36
       OR v_tenant_id NOT LIKE '________-____-____-____-____________'
       OR COALESCE(
              HEX(UNHEX(REPLACE(v_tenant_id, '-', '')))
                  <> UPPER(REPLACE(v_tenant_id, '-', '')),
              TRUE
          )
       OR p_apply IS NULL OR p_apply NOT IN ('0', '1') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSRT-001 invalid tenant GUID or apply flag';
    END IF;
    SET v_apply = (p_apply = '1');

    IF DATABASE() NOT IN ('LS_LIENS', 'LS_QA_LIENS') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSRT-002 target schema must be LS_LIENS or LS_QA_LIENS';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_lstr_reset_tables;
    CREATE TEMPORARY TABLE tmp_lstr_reset_tables (
        TableName VARCHAR(64) NOT NULL PRIMARY KEY
    );

    INSERT INTO tmp_lstr_reset_tables (TableName)
    SELECT DISTINCT c.TABLE_NAME
    FROM information_schema.COLUMNS c
    INNER JOIN information_schema.TABLES t
      ON t.TABLE_SCHEMA = c.TABLE_SCHEMA
     AND t.TABLE_NAME = c.TABLE_NAME
    WHERE c.TABLE_SCHEMA = DATABASE()
      AND t.TABLE_TYPE = 'BASE TABLE'
      AND c.COLUMN_NAME = 'TenantId'
      AND LEFT(c.TABLE_NAME, 6) = 'liens_';

    SELECT COUNT(*) INTO v_table_count FROM tmp_lstr_reset_tables;
    IF v_table_count = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSRT-003 no tenant-scoped Liens Core tables were found';
    END IF;

    -- MySQL cannot reopen one temporary table twice in the same query.  The
    -- child copy is used only for the foreign-key safety join below.
    DROP TEMPORARY TABLE IF EXISTS tmp_lstr_reset_child_tables;
    CREATE TEMPORARY TABLE tmp_lstr_reset_child_tables AS
    SELECT TableName FROM tmp_lstr_reset_tables;

    -- Every child of a reset target must itself be tenant-scoped and therefore
    -- part of the reset set.  Otherwise disabling FK checks could leave an
    -- orphaned row in a non-tenant-scoped table.
    SELECT COUNT(DISTINCT CONCAT(kcu.TABLE_NAME, ':', kcu.CONSTRAINT_NAME))
      INTO v_unscoped_fk_count
    FROM information_schema.KEY_COLUMN_USAGE kcu
    INNER JOIN tmp_lstr_reset_tables parent_table
      ON parent_table.TableName = kcu.REFERENCED_TABLE_NAME
    LEFT JOIN tmp_lstr_reset_child_tables child_table
      ON child_table.TableName = kcu.TABLE_NAME
    WHERE kcu.CONSTRAINT_SCHEMA = DATABASE()
      AND kcu.REFERENCED_TABLE_SCHEMA = DATABASE()
      AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
      AND child_table.TableName IS NULL
      AND NOT (
          (kcu.TABLE_NAME = 'liens_WorkflowStages'
           AND kcu.COLUMN_NAME = 'WorkflowConfigId'
           AND kcu.REFERENCED_TABLE_NAME = 'liens_WorkflowConfigs')
          OR
          (kcu.TABLE_NAME = 'liens_WorkflowTransitions'
           AND kcu.COLUMN_NAME = 'WorkflowConfigId'
           AND kcu.REFERENCED_TABLE_NAME = 'liens_WorkflowConfigs')
      );
    IF v_unscoped_fk_count <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSRT-004 non-tenant-scoped foreign-key children block a safe tenant reset';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_lstr_reset_summary;
    CREATE TEMPORARY TABLE tmp_lstr_reset_summary (
        TableName     VARCHAR(64) NOT NULL PRIMARY KEY,
        RowsForTenant BIGINT NOT NULL
    );

    -- These workflow children inherit their tenant through WorkflowConfigs.
    -- They are included in the plan even though they do not have TenantId.
    INSERT INTO tmp_lstr_reset_summary (TableName, RowsForTenant)
    SELECT 'liens_WorkflowTransitions', COUNT(*)
    FROM liens_WorkflowTransitions child_row
    INNER JOIN liens_WorkflowConfigs parent_row
      ON parent_row.Id = child_row.WorkflowConfigId
    WHERE parent_row.TenantId = v_tenant_id
    UNION ALL
    SELECT 'liens_WorkflowStages', COUNT(*)
    FROM liens_WorkflowStages child_row
    INNER JOIN liens_WorkflowConfigs parent_row
      ON parent_row.Id = child_row.WorkflowConfigId
    WHERE parent_row.TenantId = v_tenant_id;

    -- Record the preflight deletion plan without returning a result set for
    -- each dynamic count statement.
    SET v_done = FALSE;
    OPEN cur_reset_tables;
    SET v_cursor_open = TRUE;
    count_loop: LOOP
        FETCH cur_reset_tables INTO v_table_name;
        IF v_done THEN
            LEAVE count_loop;
        END IF;
        SET @lstr_reset_tenant = v_tenant_id;
        SET @lstr_reset_sql = CONCAT(
            'INSERT INTO tmp_lstr_reset_summary (TableName, RowsForTenant) ',
            'SELECT ', QUOTE(v_table_name), ', COUNT(*) FROM `',
            REPLACE(v_table_name, '`', '``'), '` WHERE `TenantId` = ?');
        PREPARE lstr_reset_stmt FROM @lstr_reset_sql;
        EXECUTE lstr_reset_stmt USING @lstr_reset_tenant;
        DEALLOCATE PREPARE lstr_reset_stmt;
    END LOOP;
    CLOSE cur_reset_tables;
    SET v_cursor_open = FALSE;

    IF NOT v_apply THEN
        SELECT
            'tenant-reset-preflight-passed' AS Result,
            v_tenant_id                     AS TenantId,
            v_table_count                   AS TenantScopedTables,
            SUM(RowsForTenant)              AS RowsToDelete,
            v_unscoped_fk_count             AS UnsafeForeignKeyChildren
        FROM tmp_lstr_reset_summary;
        SELECT TableName, RowsForTenant
        FROM tmp_lstr_reset_summary
        WHERE RowsForTenant <> 0
        ORDER BY TableName;
    ELSE
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        START TRANSACTION;
        SET v_in_transaction = TRUE;

        SET v_original_fk_checks = @@session.foreign_key_checks;
        SET @@session.foreign_key_checks = 0;
        SET v_fk_checks_changed = TRUE;

        -- Delete tenant-inherited workflow children before their
        -- tenant-scoped WorkflowConfig parents.
        DELETE child_row
        FROM liens_WorkflowTransitions child_row
        INNER JOIN liens_WorkflowConfigs parent_row
          ON parent_row.Id = child_row.WorkflowConfigId
        WHERE parent_row.TenantId = v_tenant_id;

        DELETE child_row
        FROM liens_WorkflowStages child_row
        INNER JOIN liens_WorkflowConfigs parent_row
          ON parent_row.Id = child_row.WorkflowConfigId
        WHERE parent_row.TenantId = v_tenant_id;

        SELECT
            (SELECT COUNT(*)
             FROM liens_WorkflowTransitions child_row
             INNER JOIN liens_WorkflowConfigs parent_row
               ON parent_row.Id = child_row.WorkflowConfigId
             WHERE parent_row.TenantId = v_tenant_id)
            +
            (SELECT COUNT(*)
             FROM liens_WorkflowStages child_row
             INNER JOIN liens_WorkflowConfigs parent_row
               ON parent_row.Id = child_row.WorkflowConfigId
             WHERE parent_row.TenantId = v_tenant_id)
          INTO v_remaining_rows;
        IF v_remaining_rows <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSRT-005 inherited workflow-child reset postcondition failed';
        END IF;

        -- Delete every tenant-owned row. FK checks are disabled only inside
        -- this transaction after the non-tenant-child safeguard above passes.
        SET v_done = FALSE;
        OPEN cur_reset_tables;
        SET v_cursor_open = TRUE;
        delete_loop: LOOP
            FETCH cur_reset_tables INTO v_table_name;
            IF v_done THEN
                LEAVE delete_loop;
            END IF;
            SET @lstr_reset_tenant = v_tenant_id;
            SET @lstr_reset_sql = CONCAT(
                'DELETE FROM `', REPLACE(v_table_name, '`', '``'),
                '` WHERE `TenantId` = ?');
            PREPARE lstr_reset_stmt FROM @lstr_reset_sql;
            EXECUTE lstr_reset_stmt USING @lstr_reset_tenant;
            DEALLOCATE PREPARE lstr_reset_stmt;
        END LOOP;
        CLOSE cur_reset_tables;
        SET v_cursor_open = FALSE;

        -- Confirm the dynamic deletion plan actually emptied every target.
        SET v_remaining_rows = 0;
        SET v_done = FALSE;
        OPEN cur_reset_tables;
        SET v_cursor_open = TRUE;
        verify_loop: LOOP
            FETCH cur_reset_tables INTO v_table_name;
            IF v_done THEN
                LEAVE verify_loop;
            END IF;
            SET @lstr_reset_tenant = v_tenant_id;
            SET @lstr_reset_sql = CONCAT(
                'SELECT COUNT(*) INTO @lstr_reset_remaining FROM `',
                REPLACE(v_table_name, '`', '``'), '` WHERE `TenantId` = ?');
            PREPARE lstr_reset_stmt FROM @lstr_reset_sql;
            EXECUTE lstr_reset_stmt USING @lstr_reset_tenant;
            DEALLOCATE PREPARE lstr_reset_stmt;
            SET v_remaining_rows = v_remaining_rows + @lstr_reset_remaining;
        END LOOP;
        CLOSE cur_reset_tables;
        SET v_cursor_open = FALSE;

        IF v_remaining_rows <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSRT-006 tenant reset postcondition failed';
        END IF;

        COMMIT;
        SET v_in_transaction = FALSE;
        SET @@session.foreign_key_checks = v_original_fk_checks;
        SET v_fk_checks_changed = FALSE;

        SELECT
            'tenant-reset-applied'          AS Result,
            v_tenant_id                     AS TenantId,
            v_table_count                   AS TenantScopedTables,
            SUM(RowsForTenant)              AS RowsDeleted,
            v_remaining_rows                AS RowsRemaining
        FROM tmp_lstr_reset_summary;
    END IF;

    SET @lstr_reset_sql = NULL;
    SET @lstr_reset_tenant = NULL;
    DROP TEMPORARY TABLE IF EXISTS tmp_lstr_reset_summary;
    DROP TEMPORARY TABLE IF EXISTS tmp_lstr_reset_child_tables;
    DROP TEMPORARY TABLE IF EXISTS tmp_lstr_reset_tables;
END$$

DELIMITER ;

-- Deploy with DBeaver "Execute SQL Script" (Alt+X).
-- Run the p_apply = '0' preflight and verify the returned table/row plan
-- before using p_apply = '1'.
