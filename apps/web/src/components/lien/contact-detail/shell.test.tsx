import { Children, cloneElement, isValidElement } from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, test, vi } from "vitest";

import { contactsService } from "@/lib/contacts";
import { ContactDetailShell } from "./shell";

const pushMock = vi.fn();
const addToastMock = vi.fn();

vi.mock("next/navigation", () => ({
  usePathname: () => "/lien/contacts/contact-123/overview",
  useRouter: () => ({ push: pushMock }),
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: React.ComponentPropsWithoutRef<"a">) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@tanstack/react-query", () => ({
  useQueryClient: () => ({ invalidateQueries: vi.fn() }),
}));

vi.mock("@/components/ui/dropdown-menu", () => ({
  DropdownMenu: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  DropdownMenuTrigger: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  DropdownMenuContent: ({ children }: { children: React.ReactNode }) => (
    <div role="menu">{children}</div>
  ),
  DropdownMenuItem: ({
    asChild,
    children,
    ...props
  }: {
    asChild?: boolean;
    children: React.ReactNode;
  }) => {
    if (asChild) {
      const child = Children.only(children);
      return isValidElement(child)
        ? cloneElement(child, { ...props, role: "menuitem" })
        : null;
    }
    return (
      <button type="button" role="menuitem" {...props}>
        {children}
      </button>
    );
  },
  DropdownMenuSeparator: () => <hr />,
}));

vi.mock("@/stores/lien-store", () => ({
  useLienStore: (selector: (state: { addToast: typeof addToastMock }) => unknown) =>
    selector({ addToast: addToastMock }),
}));

vi.mock("@/hooks/use-role-access", () => ({
  useRoleAccess: () => ({ can: () => true }),
}));

vi.mock("@/hooks/use-contacts", () => ({
  useDeleteContact: () => ({ isPending: false, mutateAsync: vi.fn() }),
}));

vi.mock("@/lib/contacts", () => ({
  CASE_REASSIGN_CONFIG: { LawFirm: {} },
  contactsService: { getContact: vi.fn() },
}));

vi.mock("@/lib/api-client", () => ({
  ApiError: class ApiError extends Error {},
}));

vi.mock("@/components/lien/add-contact-modal", () => ({
  AddContactModal: () => null,
}));

vi.mock("@/components/lien/modal", () => ({
  ConfirmDialog: () => null,
}));

const contact = {
  id: "contact-123",
  firstName: "Ada",
  lastName: "Lovelace",
  contactType: "LawFirm",
  displayName: "Ada Lovelace",
  organization: "Analytical Legal",
  email: "ada@example.test",
  phone: "555-0100",
  city: "London",
  state: "UK",
  isActive: true,
  activeCases: 2,
  createdAt: "2026-01-01T00:00:00Z",
  facilityId: null,
  lawFirmId: "law-firm-1",
  title: "Partner",
  fax: "",
  website: "",
  addressLine1: "",
  postalCode: "",
  notes: "",
  updatedAt: "2026-01-02T00:00:00Z",
  contactSubtype: null,
};

async function openActions() {
  await screen.findByRole("heading", { name: "Ada Lovelace" });
  await userEvent.click(screen.getByRole("button", { name: "Actions" }));
}

describe("ContactDetailShell Actions menu", () => {
  beforeEach(() => {
    vi.mocked(contactsService.getContact).mockResolvedValue(contact);
  });

  test("preserves the Contact actions without Open in App", async () => {
    render(
      <ContactDetailShell id="contact-123" basePath="/lien/contacts">
        <div>Overview content</div>
      </ContactDetailShell>,
    );

    await openActions();

    expect(
      screen.getAllByRole("menuitem").map((item) => item.textContent?.trim()),
    ).toEqual(["Edit Contact", "Send Email", "Delete Contact"]);
    expect(screen.getByRole("menuitem", { name: "Edit Contact" })).toBeEnabled();
    expect(screen.getByRole("menuitem", { name: "Send Email" })).toHaveAttribute(
      "href",
      "mailto:ada@example.test",
    );
    expect(screen.getByRole("separator")).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Delete Contact" })).toBeEnabled();
    expect(screen.queryByRole("menuitem", { name: "Open in App" })).not.toBeInTheDocument();
  });

  test("preserves conditional Send Email visibility without deep-link configuration", async () => {
    vi.mocked(contactsService.getContact).mockResolvedValue({
      ...contact,
      email: "",
    });

    render(
      <ContactDetailShell id="contact-123" basePath="/lien/contacts">
        <div>Overview content</div>
      </ContactDetailShell>,
    );

    await openActions();

    expect(screen.queryByRole("menuitem", { name: "Open in App" })).not.toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Edit Contact" })).toBeEnabled();
    expect(screen.queryByRole("menuitem", { name: "Send Email" })).not.toBeInTheDocument();
    expect(screen.getByRole("separator")).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Delete Contact" })).toBeEnabled();
  });
});
