import Link from "next/link";

const VIEWS = [
  { key: "companies", label: "Companies", href: "/selling/contacts/companies" },
  { key: "persons", label: "Contact Person", href: "/selling/contacts/persons" },
] as const;

export function ContactsTabs({
  active,
}: {
  active: "companies" | "persons";
}) {
  // TODO: extract this pill-tab nav into a shared component — the same
  // markup/classes are duplicated in cases/[id]/page.tsx and
  // company-detail-shell.tsx.
  return (
    <div className="flex flex-wrap items-center h-[38px] gap-1 bg-[#FAFAFA] rounded-md p-1">
      {VIEWS.map((tab) => (
        <Link
          key={tab.key}
          href={tab.href}
          className={`flex-1 h-[30px] flex items-center justify-center text-sm font-medium rounded-md transition-colors whitespace-nowrap ${
            active === tab.key
              ? "bg-[#EE7132] shadow-sm text-white"
              : "text-gray-500 hover:text-gray-700"
          }`}
        >
          {tab.label}
        </Link>
      ))}
    </div>
  );
}
