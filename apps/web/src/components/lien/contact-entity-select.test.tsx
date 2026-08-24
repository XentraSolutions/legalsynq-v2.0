import { render, screen } from "@testing-library/react";
import { describe, test, expect, beforeAll, afterEach, afterAll } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { setupServer } from "msw/node";
import { http, HttpResponse } from "msw";
import { ContactEntitySelect } from "./contact-entity-select";

/**
 * Covers the label-resolution fallback logic in ContactEntitySelect: a
 * dropdown initialized with a `value` that was never chosen through the
 * dropdown itself (e.g. loaded from saved form data) has no label for it yet
 * and must resolve one — directly by contact id, and, for MedicalFacility
 * selects specifically, via a facilityId-scoped search when the direct
 * lookup finds nothing (medical facility liens predate the unified Contacts
 * system and store a facility's own `facilityId`, not its contact id — see
 * the comment above `needsFacilityFallback` in contact-entity-select.tsx).
 *
 * MSW (msw/node) intercepts at the fetch boundary here rather than mocking
 * contactsService/contactsApi directly (vi.mock), so this exercises the real
 * query-building code in those modules (ContactsQuery -> querystring,
 * including the `ContactSubtype=""` "main contacts only" special case) —
 * the exact layer both bugs this fallback fixes actually lived in.
 */

const server = setupServer(
  // The unscoped options list every ContactEntitySelect loads on mount —
  // empty is fine, these tests only care about resolving the pre-set value.
  http.get("/api/lien/api/liens/contacts", ({ request }) => {
    const url = new URL(request.url);
    if (url.searchParams.get("FacilityId") === "legacy-facility-id") {
      return HttpResponse.json({
        items: [
          {
            id: "org-contact-id",
            firstName: "Acme",
            lastName: "Medical Facility",
            contactType: "MedicalFacility",
            displayName: "Acme Medical Facility",
            organization: null,
            email: null,
            phone: null,
            city: null,
            state: null,
            isActive: true,
            activeCases: 0,
            createdAtUtc: "2026-01-01T00:00:00Z",
            updatedAtUtc: "2026-01-01T00:00:00Z",
            contactSubtype: null,
            lawFirmId: null,
            facilityId: "legacy-facility-id",
          },
        ],
        page: 1,
        pageSize: 1,
        totalCount: 1,
      });
    }
    return HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 0 });
  }),

  http.get("/api/lien/api/liens/contacts/:id", ({ params }) => {
    if (params.id === "provider-1") {
      return HttpResponse.json({
        id: "provider-1",
        firstName: "Test",
        lastName: "Provider",
        contactType: "Provider",
        displayName: "Dr. Test Provider",
        isActive: true,
        activeCases: 0,
        createdAtUtc: "2026-01-01T00:00:00Z",
        updatedAtUtc: "2026-01-01T00:00:00Z",
      });
    }
    // "legacy-facility-id" isn't a contact id at all (it's the facility's
    // own facilityId field) — a direct lookup 404s, same as the real API.
    return HttpResponse.json({ message: "Not found" }, { status: 404 });
  }),
);

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

function renderWithQueryClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe("ContactEntitySelect — resolving a pre-set value", () => {
  test("resolves a plain contact id directly, not present in the initial options page", async () => {
    renderWithQueryClient(
      <ContactEntitySelect contactType="Provider" value="provider-1" onChange={() => {}} />,
    );

    expect(await screen.findByText("Dr. Test Provider")).toBeInTheDocument();
  });

  test("falls back to a facilityId-scoped search when the direct lookup 404s (MedicalFacility only)", async () => {
    renderWithQueryClient(
      <ContactEntitySelect
        contactType="MedicalFacility"
        value="legacy-facility-id"
        onChange={() => {}}
      />,
    );

    expect(await screen.findByText("Acme Medical Facility")).toBeInTheDocument();
  });

  test("does not attempt the facilityId fallback for non-MedicalFacility types", async () => {
    renderWithQueryClient(
      <ContactEntitySelect
        contactType="Provider"
        value="legacy-facility-id"
        onChange={() => {}}
      />,
    );

    // The 404'd id never resolves to a label for Provider — the fallback is
    // MedicalFacility-only, so the trigger keeps showing the placeholder.
    expect(await screen.findByText("Select...")).toBeInTheDocument();
    expect(screen.queryByText("Acme Medical Facility")).not.toBeInTheDocument();
  });
});
