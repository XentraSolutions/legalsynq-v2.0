-- Tenant-scoped, rerunnable lookup data for Selling bulk-import testing.
-- Adds at least five each of facilities, funding-company contacts, provider
-- contacts, facility projection contacts, facility contact persons, and
-- manual medical codes. Existing matching records are never modified.
--
-- Usage (run preflight first):
--   CALL liens_seed_selling_bulk_import_lookups(
--     'tenant-guid', 'seller-org-guid', 'user-guid', '0');
-- Then apply only after confirming the preflight counts:
--   CALL liens_seed_selling_bulk_import_lookups(
--     'tenant-guid', 'seller-org-guid', 'user-guid', '1');

DROP PROCEDURE IF EXISTS liens_seed_selling_bulk_import_lookups;

DELIMITER $$

CREATE PROCEDURE liens_seed_selling_bulk_import_lookups(
    IN p_tenant_id CHAR(36),
    IN p_org_id CHAR(36),
    IN p_user_id CHAR(36),
    IN p_apply CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36);
    DECLARE v_org_id CHAR(36);
    DECLARE v_user_id CHAR(36);
    DECLARE v_apply BOOLEAN;
    DECLARE v_facilities_inserted INT DEFAULT 0;
    DECLARE v_funding_contacts_inserted INT DEFAULT 0;
    DECLARE v_provider_contacts_inserted INT DEFAULT 0;
    DECLARE v_facility_contacts_inserted INT DEFAULT 0;
    DECLARE v_facility_people_inserted INT DEFAULT 0;
    DECLARE v_medical_codes_inserted INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        DROP TEMPORARY TABLE IF EXISTS tmp_selling_bulk_seed;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_org_id = LOWER(TRIM(p_org_id));
    SET v_user_id = LOWER(TRIM(p_user_id));
    SET v_apply = p_apply = '1';

    IF DATABASE() NOT IN ('LS_LIENS', 'LS_QA_LIENS') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'SBLK-001 target schema must be LS_LIENS or LS_QA_LIENS';
    END IF;
    IF v_tenant_id IS NULL OR v_org_id IS NULL OR v_user_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR v_org_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR v_user_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL OR p_apply NOT IN ('0', '1') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'SBLK-002 tenant, org, user, and apply flag must be valid';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_selling_bulk_seed;
    CREATE TEMPORARY TABLE tmp_selling_bulk_seed (
        SequenceNo INT NOT NULL PRIMARY KEY,
        FacilityId CHAR(36) NOT NULL,
        FacilityName VARCHAR(200) NOT NULL,
        FacilityCode VARCHAR(50) NOT NULL,
        FacilityEmail VARCHAR(320) NOT NULL,
        FundingCompanyName VARCHAR(200) NOT NULL,
        ProviderName VARCHAR(200) NOT NULL,
        ContactFirstName VARCHAR(100) NOT NULL,
        ContactLastName VARCHAR(100) NOT NULL,
        ContactEmail VARCHAR(320) NOT NULL,
        MedicalCode VARCHAR(50) NOT NULL,
        MedicalDescription VARCHAR(255) NOT NULL,
        MedicalCost DECIMAL(18,2) NOT NULL
    );

    INSERT INTO tmp_selling_bulk_seed
        (SequenceNo, FacilityId, FacilityName, FacilityCode, FacilityEmail, FundingCompanyName, ProviderName,
         ContactFirstName, ContactLastName, ContactEmail, MedicalCode, MedicalDescription, MedicalCost)
    VALUES
        (1, UUID(), 'Demo Medical Facility', 'BULK-FAC-01', 'facility01@example.test', 'Demo Funding Company', 'Demo Medical Provider', 'Jamie', 'Smith', 'jamie.smith@example.test', '45385', 'Colonoscopy, flexible; with removal by snare technique', 879.00),
        (2, UUID(), 'Example Medical Center', 'BULK-FAC-02', 'facility02@example.test', 'Example Funding Co.', 'Example Medical Center', 'Avery', 'Jones', 'avery.jones@example.test', '99213', 'Office or other outpatient visit', 82.00),
        (3, UUID(), 'Nae Medical Center', 'BULK-FAC-03', 'facility03@example.test', 'Nae Funding', 'Nae Medical Provider', 'Morgan', 'Lee', 'morgan.lee@example.test', '99214', 'Established patient office visit', 105.00),
        (4, UUID(), 'Synq Test Imaging', 'BULK-FAC-04', 'facility04@example.test', 'Synq Test Funding 04', 'Synq Test Provider 04', 'Jordan', 'Reyes', 'jordan.reyes@example.test', '72148', 'MRI lumbar spine without contrast', 410.00),
        (5, UUID(), 'Synq Test Surgery Center', 'BULK-FAC-05', 'facility05@example.test', 'Synq Test Funding 05', 'Synq Test Provider 05', 'Taylor', 'Cruz', 'taylor.cruz@example.test', '29881', 'Knee arthroscopy with meniscectomy', 650.00);

    IF NOT v_apply THEN
        SELECT
            (SELECT COUNT(*) FROM tmp_selling_bulk_seed seed
             WHERE NOT EXISTS (SELECT 1 FROM liens_Facilities facility
                               WHERE facility.TenantId = v_tenant_id AND facility.OrgId = v_org_id
                                 AND facility.Name = seed.FacilityName)) AS FacilitiesToInsert,
            (SELECT COUNT(*) FROM tmp_selling_bulk_seed seed
             WHERE NOT EXISTS (SELECT 1 FROM liens_Contacts contact
                               WHERE contact.TenantId = v_tenant_id AND contact.OrgId = v_org_id
                                 AND contact.ContactType = 'FundingCompany'
                                 AND contact.Organization = seed.FundingCompanyName)) AS FundingContactsToInsert,
            (SELECT COUNT(*) FROM tmp_selling_bulk_seed seed
             WHERE NOT EXISTS (SELECT 1 FROM liens_Contacts contact
                               WHERE contact.TenantId = v_tenant_id AND contact.OrgId = v_org_id
                                 AND contact.ContactType = 'Provider'
                                 AND contact.Organization = seed.ProviderName)) AS ProviderContactsToInsert,
            (SELECT COUNT(*) FROM tmp_selling_bulk_seed seed
             WHERE NOT EXISTS (SELECT 1 FROM liens_Contacts contact
                               WHERE contact.TenantId = v_tenant_id AND contact.OrgId = v_org_id
                                 AND contact.ContactType = 'MedicalFacility' AND contact.FacilityId = seed.FacilityId
                                 AND (contact.ContactSubtype IS NULL OR contact.ContactSubtype = ''))) AS FacilityProjectionContactsToInsert,
            (SELECT COUNT(*) FROM tmp_selling_bulk_seed seed
             WHERE NOT EXISTS (SELECT 1 FROM liens_FacilityContactPersons person
                               WHERE person.TenantId = v_tenant_id AND person.FacilityId = seed.FacilityId
                                 AND person.Email = seed.ContactEmail)) AS FacilityContactPersonsToInsert,
            (SELECT COUNT(*) FROM tmp_selling_bulk_seed seed
             WHERE NOT EXISTS (SELECT 1 FROM liens_ManualMedicalCodes code
                               WHERE code.TenantId = v_tenant_id AND code.Code = seed.MedicalCode)) AS MedicalCodesToInsert;
        DROP TEMPORARY TABLE IF EXISTS tmp_selling_bulk_seed;
        SELECT 'selling-bulk-seed-preflight-completed' AS Result;
    ELSE
        START TRANSACTION;

        INSERT INTO liens_Facilities
            (Id, TenantId, OrgId, Name, Code, ExternalReference, AddressLine1, City, State, PostalCode,
             Phone, Email, Fax, IsActive, OrganizationId, CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId)
        SELECT seed.FacilityId, v_tenant_id, v_org_id, seed.FacilityName, seed.FacilityCode,
               CONCAT('selling-bulk-seed:', seed.SequenceNo), CONCAT(seed.SequenceNo, ' Test Avenue'),
               'Austin', 'TX', '78701', '512-555-01', seed.FacilityEmail, NULL, 1, NULL,
               UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), v_user_id, v_user_id
        FROM tmp_selling_bulk_seed seed
        WHERE NOT EXISTS (SELECT 1 FROM liens_Facilities facility
                          WHERE facility.TenantId = v_tenant_id AND facility.OrgId = v_org_id
                            AND facility.Name = seed.FacilityName);
        SET v_facilities_inserted = ROW_COUNT();

        UPDATE tmp_selling_bulk_seed seed
        INNER JOIN liens_Facilities facility
            ON facility.TenantId = v_tenant_id AND facility.OrgId = v_org_id
           AND facility.Name = seed.FacilityName
        SET seed.FacilityId = facility.Id;

        INSERT INTO liens_Contacts
            (Id, TenantId, OrgId, ContactType, ContactSubtype, FirstName, LastName, DisplayName,
             Title, Organization, Email, Phone, Fax, Website, AddressLine1, City, State, PostalCode,
             Notes, IsActive, CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId, FacilityId, LawFirmId)
        SELECT UUID(), v_tenant_id, v_org_id, 'FundingCompany', NULL, 'Bulk', CONCAT('Funding ', seed.SequenceNo),
               CONCAT('Bulk Funding ', seed.SequenceNo), 'Funding Company', seed.FundingCompanyName,
               CONCAT('funding', seed.SequenceNo, '@example.test'), '512-555-10', NULL, NULL, NULL, NULL, NULL, NULL,
               CONCAT('seed=selling-bulk-import; sequence=', seed.SequenceNo), 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6),
               v_user_id, v_user_id, NULL, NULL
        FROM tmp_selling_bulk_seed seed
        WHERE NOT EXISTS (SELECT 1 FROM liens_Contacts contact
                          WHERE contact.TenantId = v_tenant_id AND contact.OrgId = v_org_id
                            AND contact.ContactType = 'FundingCompany'
                            AND contact.Organization = seed.FundingCompanyName);
        SET v_funding_contacts_inserted = ROW_COUNT();

        INSERT INTO liens_Contacts
            (Id, TenantId, OrgId, ContactType, ContactSubtype, FirstName, LastName, DisplayName,
             Title, Organization, Email, Phone, Fax, Website, AddressLine1, City, State, PostalCode,
             Notes, IsActive, CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId, FacilityId, LawFirmId)
        SELECT UUID(), v_tenant_id, v_org_id, 'Provider', NULL, 'Bulk', CONCAT('Provider ', seed.SequenceNo),
               CONCAT('Bulk Provider ', seed.SequenceNo), 'Medical Provider', seed.ProviderName,
               CONCAT('provider', seed.SequenceNo, '@example.test'), '512-555-20', NULL, NULL, NULL, NULL, NULL, NULL,
               CONCAT('seed=selling-bulk-import; sequence=', seed.SequenceNo), 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6),
               v_user_id, v_user_id, NULL, NULL
        FROM tmp_selling_bulk_seed seed
        WHERE NOT EXISTS (SELECT 1 FROM liens_Contacts contact
                          WHERE contact.TenantId = v_tenant_id AND contact.OrgId = v_org_id
                            AND contact.ContactType = 'Provider'
                            AND contact.Organization = seed.ProviderName);
        SET v_provider_contacts_inserted = ROW_COUNT();

        INSERT INTO liens_Contacts
            (Id, TenantId, OrgId, ContactType, ContactSubtype, FirstName, LastName, DisplayName,
             Title, Organization, Email, Phone, Fax, Website, AddressLine1, City, State, PostalCode,
             Notes, IsActive, CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId, FacilityId, LawFirmId)
        SELECT UUID(), v_tenant_id, v_org_id, 'MedicalFacility', NULL, 'Bulk', CONCAT('Facility ', seed.SequenceNo),
               seed.FacilityName, 'Facility', seed.FacilityName, seed.FacilityEmail, '512-555-01', NULL, NULL,
               NULL, 'Austin', 'TX', '78701', CONCAT('seed=selling-bulk-import; sequence=', seed.SequenceNo), 1,
               UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), v_user_id, v_user_id, seed.FacilityId, NULL
        FROM tmp_selling_bulk_seed seed
        WHERE NOT EXISTS (SELECT 1 FROM liens_Contacts contact
                          WHERE contact.TenantId = v_tenant_id AND contact.OrgId = v_org_id
                            AND contact.ContactType = 'MedicalFacility' AND contact.FacilityId = seed.FacilityId
                            AND (contact.ContactSubtype IS NULL OR contact.ContactSubtype = ''));
        SET v_facility_contacts_inserted = ROW_COUNT();

        INSERT INTO liens_FacilityContactPersons
            (Id, TenantId, FacilityId, FirstName, LastName, Position, Email, Phone, IsActive,
             CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId)
        SELECT UUID(), v_tenant_id, seed.FacilityId, seed.ContactFirstName, seed.ContactLastName,
               'Billing Contact', seed.ContactEmail, '512-555-30', 1,
               UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), v_user_id, v_user_id
        FROM tmp_selling_bulk_seed seed
        WHERE NOT EXISTS (SELECT 1 FROM liens_FacilityContactPersons person
                          WHERE person.TenantId = v_tenant_id AND person.FacilityId = seed.FacilityId
                            AND person.Email = seed.ContactEmail);
        SET v_facility_people_inserted = ROW_COUNT();

        INSERT INTO liens_ManualMedicalCodes
            (Id, TenantId, Code, Description, FacilityType, Cost, Copay, FacilityTotal, PhysicianTotal,
             Total, Status, CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId)
        SELECT UUID(), v_tenant_id, seed.MedicalCode, seed.MedicalDescription, 'ASC', seed.MedicalCost,
               0.00, seed.MedicalCost, 0.00, seed.MedicalCost, 'A', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6),
               v_user_id, v_user_id
        FROM tmp_selling_bulk_seed seed
        WHERE NOT EXISTS (SELECT 1 FROM liens_ManualMedicalCodes code
                          WHERE code.TenantId = v_tenant_id AND code.Code = seed.MedicalCode);
        SET v_medical_codes_inserted = ROW_COUNT();

        COMMIT;
        DROP TEMPORARY TABLE IF EXISTS tmp_selling_bulk_seed;
        SELECT 'selling-bulk-seed-applied' AS Result,
               v_facilities_inserted AS FacilitiesInserted,
               v_funding_contacts_inserted AS FundingContactsInserted,
               v_provider_contacts_inserted AS ProviderContactsInserted,
               v_facility_contacts_inserted AS FacilityProjectionContactsInserted,
               v_facility_people_inserted AS FacilityContactPersonsInserted,
               v_medical_codes_inserted AS MedicalCodesInserted;
    END IF;
END$$

DELIMITER ;
