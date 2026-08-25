import { http, HttpResponse } from "msw";

/**
 * Fixture ids/names for the mocked lien case detail page
 * (e2e/mocked/lien-facility-provider-contact.spec.ts). Exported so the spec
 * can assert against the same values these handlers return.
 *
 * FACILITY_LEGACY_ID is deliberately a *different* id than
 * FACILITY_CONTACT_ID — mirrors the real facilityId-vs-contact-id split
 * this fixture exists to prove out (see the comment on
 * needsFacilityFallback in src/components/lien/contact-entity-select.tsx):
 * get-facility's `facilityId` field is the contact's `facilityId` property,
 * not its own `id`, so resolving it requires the fallback path, not a
 * direct by-id lookup.
 */
export const MOCK_CASE_ID = "case-mock-001";
export const MOCK_LIEN_ID = "lien-mock-001";

export const FACILITY_CONTACT_ID = "contact-facility-001";
export const FACILITY_LEGACY_ID = "legacy-facility-999";
export const FACILITY_DISPLAY_NAME = "Mocked Medical Facility";

export const CONTACT_PERSON_ID = "contact-person-001";
export const CONTACT_PERSON_DISPLAY_NAME = "Jamie Mocked";

export const PROVIDER_ID = "contact-provider-001";
export const PROVIDER_DISPLAY_NAME = "Dr. Mocked Provider";

interface MockContact {
  id: string;
  firstName: string;
  lastName: string;
  contactType: string;
  displayName: string;
  organization: string | null;
  email: string | null;
  phone: string | null;
  city: string | null;
  state: string | null;
  isActive: boolean;
  activeCases: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  contactSubtype: string | null;
  lawFirmId: string | null;
  facilityId: string | null;
}

const mockCase = {
  id: MOCK_CASE_ID,
  caseNumber: "26-MOCK001",
  clientFirstName: "Mock",
  clientLastName: "Client",
  clientDisplayName: "Mock Client",
  status: "Pre-Demand",
  clientStreetAddress: "",
  clientCity: "",
  clientState: "",
  clientZipcode: "",
  sex: "",
  caseType: "Motor Vehicle Accident",
  currentMedicalStatus: "",
  stateOfIncident: "AL",
  trackingFollowUpDate: "",
  leadId: "",
  lawFirm: "Mock Law Firm",
  caseManager: "",
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: "2026-01-01T00:00:00Z",
};

const mockFacilityContact: MockContact = {
  id: FACILITY_CONTACT_ID,
  firstName: "Mocked Medical",
  lastName: "Facility",
  contactType: "MedicalFacility",
  displayName: FACILITY_DISPLAY_NAME,
  organization: null,
  email: "facility@example.com",
  phone: "(555) 555-0100",
  city: null,
  state: null,
  isActive: true,
  activeCases: 1,
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: "2026-01-01T00:00:00Z",
  contactSubtype: null,
  lawFirmId: null,
  facilityId: FACILITY_LEGACY_ID,
};

const mockContactPerson: MockContact = {
  id: CONTACT_PERSON_ID,
  firstName: "Jamie",
  lastName: "Mocked",
  contactType: "MedicalFacility",
  displayName: CONTACT_PERSON_DISPLAY_NAME,
  organization: null,
  email: null,
  phone: "(555) 555-0101",
  city: null,
  state: null,
  isActive: true,
  activeCases: 0,
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: "2026-01-01T00:00:00Z",
  contactSubtype: "FacilityContactPerson",
  lawFirmId: null,
  facilityId: FACILITY_CONTACT_ID,
};

const mockProvider: MockContact = {
  id: PROVIDER_ID,
  firstName: "Dr. Mocked",
  lastName: "Provider",
  contactType: "Provider",
  displayName: PROVIDER_DISPLAY_NAME,
  organization: null,
  email: null,
  phone: "(555) 555-0102",
  city: null,
  state: null,
  isActive: true,
  activeCases: 0,
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: "2026-01-01T00:00:00Z",
  contactSubtype: null,
  lawFirmId: null,
  facilityId: null,
};

const contactsById: Record<string, MockContact> = {
  [CONTACT_PERSON_ID]: mockContactPerson,
  [PROVIDER_ID]: mockProvider,
  // FACILITY_LEGACY_ID is intentionally absent — it's not a contact id (see
  // the module comment), so a direct by-id lookup must 404 and force the
  // facilityId-scoped fallback in ContactEntitySelect.
};

const emptyLookup = { category: "", isActive: true, isSystem: true, sortOrder: 0 };

export const lienHandlers = [
  http.get("*/liens/api/liens/cases/:id", () => HttpResponse.json(mockCase)),

  // lookupApi.getDocumentType() types its response as a flat array, not the
  // {isSuccess,message,data} envelope the /liens/api/liens/cases/... family
  // uses — mismatching this breaks CaseDetailShell's shared `error` state
  // (fetchDocumentTypes failing blanks the whole page, not just this list).
  http.get("*/liens/lookup/document/type", () =>
    HttpResponse.json([
      { id: "doc-type-1", code: "LienAgreement", name: "Lien Agreement", description: "", ...emptyLookup },
    ]),
  ),

  http.post("*/liens/api/liens/cases/case-updates/v3", () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 10, totalCount: 0 }),
  ),

  http.post("*/liens/api/liens/cases/get-notes", () =>
    HttpResponse.json({ isSuccess: true, message: "ok", data: [] }),
  ),

  http.get("*/liens/lookup/all", () =>
    HttpResponse.json({
      AccidentType: [],
      CaseStatus: [],
      ContactType: [],
      CurrentAttributes: [],
      DocumentCategory: [],
      LienStatus: [{ id: "status-active", code: "Active", name: "Active", ...emptyLookup }],
      LienType: [],
      MedicalStatus: [],
      ProcedureCode: [],
      ServicingPriority: [],
      ServicingStatus: [],
      SettlementStatus: [],
      SettlementType: [],
      State: [],
    }),
  ),

  http.get("*/liens/api/liens/cases/liens/get-medical/:lienId", () =>
    HttpResponse.json({
      isSuccess: true,
      message: "Successfully retrieved medical information.",
      data: {
        id: MOCK_LIEN_ID,
        caseId: MOCK_CASE_ID,
        status: "Active",
        purchaseDate: "2026-01-01T00:00:00Z",
        initialServiceDate: "2026-01-01T00:00:00Z",
        endServiceDate: null,
        note: "",
        isBulk: "No",
        isServicing: "No",
        fundingCompany: "",
        fundingCompanyId: "",
      },
    }),
  ),

  // The core of this fixture: facilityId/facilityContactId/medicalProviderId
  // all populated — proving the display-resolution fix once the backend
  // actually persists these (it currently doesn't; see the "known backend
  // gap" comment in e2e/(platform)/lien/mutations/facility-provider-contact.spec.ts).
  http.get("*/liens/api/liens/cases/liens/get-facility/:lienId", () =>
    HttpResponse.json({
      isSuccess: true,
      message: "Successfully retrieved medical information.",
      data: {
        id: "",
        liensId: MOCK_LIEN_ID,
        facilityId: FACILITY_LEGACY_ID,
        facilityContactId: CONTACT_PERSON_ID,
        email: "facility@example.com",
        phone: "",
        medicalProviderId: PROVIDER_ID,
        created: "2026-01-01T00:00:00Z",
        createdBy: "",
        updated: "2026-01-01T00:00:00Z",
        updatedBy: "",
      },
    }),
  ),

  http.get("*/liens/api/liens/cases/liens/get-medicalcode/:lienId", () =>
    HttpResponse.json({ isSuccess: true, message: "ok", data: [] }),
  ),

  http.get("*/liens/api/liens/cases/liens/get-medicaldocument/:lienId", () =>
    HttpResponse.json({ isSuccess: true, message: "ok", data: [] }),
  ),

  http.get("*/liens/api/liens/cases/liens/get-payee-outbound/:lienId", () =>
    HttpResponse.json({ isSuccess: true, message: "ok", data: { payee: "", outboundCheckNumber: "" } }),
  ),

  // Backs every ContactEntitySelect on the page (Facility Name, Select
  // Contact Person, Provider Name, Funding Company) plus the
  // facilityId-scoped fallback query — branches on query params rather than
  // one handler per shape, since they all hit this same path.
  http.get("*/liens/api/liens/contacts", ({ request }) => {
    const url = new URL(request.url);
    const contactType = url.searchParams.get("ContactType");
    const contactSubtype = url.searchParams.get("ContactSubtype");
    const facilityId = url.searchParams.get("FacilityId");

    let items: MockContact[] = [];
    if (facilityId) {
      // Facility-scoped queries: "" means "main contact only" (the org
      // itself, resolved via the fallback path), a real subtype value means
      // its sub-contacts (e.g. the contact person).
      if (contactSubtype === "FacilityContactPerson") {
        items = facilityId === FACILITY_CONTACT_ID ? [mockContactPerson] : [];
      } else {
        items = facilityId === FACILITY_LEGACY_ID ? [mockFacilityContact] : [];
      }
    } else if (contactType === "MedicalFacility") {
      items = [mockFacilityContact];
    } else if (contactType === "Provider") {
      items = [mockProvider];
    }

    return HttpResponse.json({ items, page: 1, pageSize: 25, totalCount: items.length });
  }),

  http.get("*/liens/api/liens/contacts/:id", ({ params }) => {
    const contact = contactsById[params.id as string];
    if (!contact) {
      return HttpResponse.json({ message: "Not found" }, { status: 404 });
    }
    return HttpResponse.json(contact);
  }),
];
