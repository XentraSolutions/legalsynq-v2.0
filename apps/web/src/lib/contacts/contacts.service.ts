import { contactsApi } from "./contacts.api";
import { lookupApi } from "../lookup/lookup.api";
import { casesApi } from "../cases/cases.api";
import { liensApi } from "../liens/liens.api";
import type {
  CaseResponseDto,
  CaseListApiResponse,
  ContactCaseLookupParams,
  LienResponseDto,
  BatchReassignCasesRequestDto,
} from "../cases/cases.types";
import { formatDateField } from "../cases/cases.mapper";
import {
  mapContactToListItem,
  mapContactToDetail,
  mapContactPagination,
} from "./contacts.mapper";
import type {
  ContactsQuery,
  ContactListItem,
  ContactDetail,
  ContactCaseSummary,
  PaginationMeta,
  CreateContactRequestDto,
  UpdateContactRequestDto,
  ExportResponse,
} from "./contacts.types";

// Legacy cases are looked up through per-type endpoints, keyed by the
// contact's own id (e.g. a LawFirm contact's id doubles as its lawFirmId).
// Contact types with no known case-lookup endpoint simply return no cases.
const CASE_LOOKUP_BY_CONTACT_TYPE: Record<
  string,
  (
    id: string,
    params: ContactCaseLookupParams,
  ) => Promise<{ data: CaseListApiResponse }>
> = {
  LawFirm: (id, params) => casesApi.listByLawFirm(id, params),
  Lead: (id, params) => casesApi.listByLead(id, params),
  MedicalFacility: (id, params) => casesApi.listByFacility(id, params),
  Provider: (id, params) => casesApi.listByMedicalProvider(id, params),
  FundingCompany: (id, params) => casesApi.listByFundingCompany(id, params),
};

// Contact types whose reassign target is a lien, not the case itself
// (MedicalFacility/Provider/FundingCompany). These have no lien id or
// amounts on the case record, so the liens have to be fetched per case.
const LIEN_LEVEL_CONTACT_TYPES = new Set([
  "MedicalFacility",
  "Provider",
  "FundingCompany",
]);

function baseCaseFields(dto: CaseResponseDto) {
  return {
    caseNumber: dto.caseNumber,
    personName:
      dto.clientDisplayName ||
      `${dto.clientFirstName} ${dto.clientLastName}`.trim(),
    accidentType: dto.caseType || null,
    dateOfLoss: dto.dateOfIncident ? formatDateField(dto.dateOfIncident) : null,
    dateOfBirth: dto.clientDob ? formatDateField(dto.clientDob) : null,
    status: dto.status,
  };
}

function mapCaseToContactSummary(dto: CaseResponseDto): ContactCaseSummary {
  return {
    id: dto.id,
    ...baseCaseFields(dto),
    lienId: null,
    billingAmount: null,
    purchaseAmount: null,
  };
}

function mapLienToContactCaseSummary(
  caseDto: CaseResponseDto,
  lien: LienResponseDto,
): ContactCaseSummary {
  return {
    id: caseDto.id,
    ...baseCaseFields(caseDto),
    lienId: lien.id,
    billingAmount: lien.originalAmount ?? null,
    purchaseAmount: lien.purchaseAmount ?? lien.purchasePrice ?? null,
  };
}

export interface ContactListResult {
  items: ContactListItem[];
  pagination: PaginationMeta;
}

// Case Managers are Law Firm contacts distinguished by their contactSubtype
// role (see LawFirmContactSection), not a distinct top-level contactType.
// The role code itself is tenant-configurable, so it's resolved from the
// lawfirm/role lookup rather than hardcoded, and cached for the session.
let caseManagerRoleCodePromise: Promise<string | undefined> | null = null;

async function resolveCaseManagerRoleCode(): Promise<string | undefined> {
  if (!caseManagerRoleCodePromise) {
    caseManagerRoleCodePromise = lookupApi
      .getLawFirmContactRoles()
      .then(({ data }) => {
        const match = data.find(
          (r) =>
            r.code.toLowerCase() === "casemanager" ||
            r.name.toLowerCase() === "case manager",
        );
        return match?.code;
      })
      .catch(() => undefined);
  }
  return caseManagerRoleCodePromise;
}

export interface CaseReassignSecondaryConfig {
  label: string;
  contactType: string;
  // Resolved lazily (case manager's subtype code is tenant-configurable).
  getContactSubtype: () => Promise<string | undefined>;
  scopeParam: "lawFirmId" | "facilityId";
}

export interface CaseReassignConfig {
  // "case" reassigns the case record itself (law firm, lead); "lien"
  // reassigns every lien under the case individually, since the backend has
  // no case-level endpoint for facility/funding/provider contacts.
  level: "case" | "lien";
  label: string;
  // Dropdown label, matching the legacy "Re-Assign Case" modal copy (e.g.
  // "Available Medical Facility", or "Leads" for the one type that doesn't
  // follow the "Available X" phrasing).
  fieldLabel: string;
  secondary?: CaseReassignSecondaryConfig;
}

// Mirrors CASE_LOOKUP_BY_CONTACT_TYPE's keys — same contact types that
// surface a Cases table on the contact detail page are reassignable here.
export const CASE_REASSIGN_CONFIG: Record<string, CaseReassignConfig> = {
  LawFirm: {
    level: "case",
    label: "Law Firm",
    fieldLabel: "Available Law Firm",
    secondary: {
      label: "Case Manager",
      contactType: "LawFirm",
      getContactSubtype: () => resolveCaseManagerRoleCode(),
      scopeParam: "lawFirmId",
    },
  },
  Lead: {
    level: "case",
    label: "Lead",
    fieldLabel: "Leads",
  },
  MedicalFacility: {
    level: "lien",
    label: "Medical Facility",
    fieldLabel: "Available Medical Facility",
    secondary: {
      label: "Medical Facility Contact",
      contactType: "MedicalFacility",
      getContactSubtype: async () => "FacilityContactPerson",
      scopeParam: "facilityId",
    },
  },
  Provider: {
    level: "lien",
    label: "Medical Provider",
    fieldLabel: "Available Medical Provider",
  },
  FundingCompany: {
    level: "lien",
    label: "Funding Company",
    fieldLabel: "Available Funding Company",
  },
};

export const contactsService = {
  async getContacts(query: ContactsQuery = {}): Promise<ContactListResult> {
    const { data } = await contactsApi.list(query);
    return {
      items: data.items.map(mapContactToListItem),
      pagination: mapContactPagination(data),
    };
  },

  async getCaseManagerRoleCode(): Promise<string | undefined> {
    return resolveCaseManagerRoleCode();
  },

  async getCaseManagers(
    params: { lawFirmId?: string } = {},
  ): Promise<ContactListResult> {
    const contactSubtype = await resolveCaseManagerRoleCode();
    const { data } = await contactsApi.list({
      ContactType: "LawFirm",
      ContactSubtype: contactSubtype,
      LawFirmId: params.lawFirmId,
    });
    return {
      items: data.items.map(mapContactToListItem),
      pagination: mapContactPagination(data),
    };
  },

  async getContact(id: string): Promise<ContactDetail> {
    const { data } = await contactsApi.getById(id);
    return mapContactToDetail(data);
  },

  async createContact(
    request: CreateContactRequestDto,
  ): Promise<ContactDetail> {
    const { data } = await contactsApi.create(request);
    return mapContactToDetail(data);
  },

  async updateContact(
    id: string,
    request: UpdateContactRequestDto,
  ): Promise<ContactDetail> {
    const { data } = await contactsApi.update(id, request);
    return mapContactToDetail(data);
  },

  async deactivateContact(id: string): Promise<ContactDetail> {
    const { data } = await contactsApi.deactivate(id);
    return mapContactToDetail(data);
  },

  async reactivateContact(id: string): Promise<ContactDetail> {
    const { data } = await contactsApi.reactivate(id);
    return mapContactToDetail(data);
  },

  async deleteContact(id: string): Promise<unknown> {
    const { data } = await contactsApi.delete(id);
    return data;
  },

  async exportContacts(contactType: string): Promise<ExportResponse> {
    const { data } = await contactsApi.export(contactType);
    return data;
  },

  async exportFacilityContacts(contactType: string): Promise<ExportResponse> {
    const { data } = await contactsApi.exportFacility(contactType);
    return data;
  },

  async getCasesByContact(
    contactId: string,
    contactType: string,
    params: ContactCaseLookupParams,
  ): Promise<{ items: ContactCaseSummary[]; totalCount: number }> {
    const lookup = CASE_LOOKUP_BY_CONTACT_TYPE[contactType];
    if (!lookup) return { items: [], totalCount: 0 };

    const { data } = await lookup(contactId, params);
    const cases = data.data ?? [];

    if (!LIEN_LEVEL_CONTACT_TYPES.has(contactType)) {
      return { items: cases.map(mapCaseToContactSummary), totalCount: data.totalCount };
    }

    const rowsByCase = await Promise.all(
      cases.map(async (caseDto) => {
        const { data: lienData } = await casesApi.listLiensByCase({
          CaseId: caseDto.id,
          page: 1,
          limit: 50,
        });
        return (lienData.items ?? []).map((lien) =>
          mapLienToContactCaseSummary(caseDto, lien),
        );
      }),
    );
    return { items: rowsByCase.flat(), totalCount: data.totalCount };
  },

  // Moves every case (and their liens) from one contact to another of the
  // same contactType in a single backend call.
  async batchReassignCases(
    request: BatchReassignCasesRequestDto,
  ): Promise<unknown> {
    const { data } = await casesApi.batchReassign(request);
    return data;
  },

  // Reassigns a single case (or, for lien-level contact types, a single
  // lien) to a new contact of the same type as `contactType`. Law firm and
  // medical facility reassignment cascade to a required secondary contact
  // (case manager / facility contact person) per CASE_REASSIGN_CONFIG.
  async reassignCase(params: {
    contactType: string;
    item: ContactCaseSummary;
    newPrimaryId: string;
    newSecondaryId?: string;
  }): Promise<void> {
    const { contactType, item, newPrimaryId, newSecondaryId } = params;

    switch (contactType) {
      case "LawFirm":
        await casesApi.reassignLawFirm({
          caseId: item.id,
          lawfirm: newPrimaryId,
        });
        if (newSecondaryId) {
          await casesApi.reassignCaseManager({
            caseId: item.id,
            caseManager: newSecondaryId,
          });
        }
        return;

      case "Lead":
        await casesApi.reassignLead({
          caseId: item.id,
          leadId: newPrimaryId,
        });
        return;

      case "MedicalFacility":
        if (!item.lienId) throw new Error("Missing lien for this row.");
        await liensApi.reassignFacility({
          liensId: item.lienId,
          facility: newPrimaryId,
        });
        if (newSecondaryId) {
          await liensApi.reassignContactPerson({
            liensId: item.lienId,
            facilityContactPerson: newSecondaryId,
          });
        }
        return;

      case "Provider":
        if (!item.lienId) throw new Error("Missing lien for this row.");
        await liensApi.reassignMedicalProvider({
          liensId: item.lienId,
          medicalProvider: newPrimaryId,
        });
        return;

      case "FundingCompany":
        if (!item.lienId) throw new Error("Missing lien for this row.");
        await liensApi.reassignFundingCompany({
          liensId: item.lienId,
          fundingCompany: newPrimaryId,
        });
        return;

      default:
        throw new Error(
          `Reassign is not supported for contact type "${contactType}".`,
        );
    }
  },
};
