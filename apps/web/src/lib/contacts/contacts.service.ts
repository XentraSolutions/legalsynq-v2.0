import { contactsApi } from "./contacts.api";
import { lookupApi } from "../lookup/lookup.api";
import { casesApi } from "../cases/cases.api";
import type {
  CaseResponseDto,
  CaseListApiResponse,
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
  (id: string) => Promise<{ data: CaseListApiResponse }>
> = {
  LawFirm: (id) => casesApi.listByLawFirm(id),
  Lead: (id) => casesApi.listByLead(id),
  MedicalFacility: (id) => casesApi.listByFacility(id),
  Provider: (id) => casesApi.listByMedicalProvider(id),
  FundingCompany: (id) => casesApi.listByFundingCompany(id),
};

function mapCaseToContactSummary(dto: CaseResponseDto): ContactCaseSummary {
  return {
    id: dto.id,
    caseNumber: dto.caseNumber,
    personName:
      dto.clientDisplayName ||
      `${dto.clientFirstName} ${dto.clientLastName}`.trim(),
    accidentType: dto.caseType || null,
    dateOfLoss: dto.dateOfIncident ? formatDateField(dto.dateOfIncident) : null,
    dateOfBirth: dto.clientDob ? formatDateField(dto.clientDob) : null,
    status: dto.status,
    lienId: null,
    billingAmount: null,
    purchaseAmount: null,
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

  async getCasesByContact(
    contactId: string,
    contactType: string,
  ): Promise<ContactCaseSummary[]> {
    const lookup = CASE_LOOKUP_BY_CONTACT_TYPE[contactType];
    if (!lookup) return [];

    const { data } = await lookup(contactId);
    return (data.data ?? []).map(mapCaseToContactSummary);
  },
};
