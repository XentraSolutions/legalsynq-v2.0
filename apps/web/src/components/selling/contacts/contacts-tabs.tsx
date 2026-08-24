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
  return (
    <div className="flex flex-wrap gap-1 bg-[#FAFAFA] rounded-md p-1">
      {VIEWS.map((tab) => (
        <Link
          key={tab.key}
          href={tab.href}
          className={`flex-1 text-center px-4 py-2 text-sm font-medium rounded-lg transition-colors whitespace-nowrap ${
            active === tab.key
              ? "bg-white shadow-sm text-gray-900"
              : "text-gray-500 hover:text-gray-700"
          }`}
        >
          {tab.label}
        </Link>
      ))}
    </div>
  );
}
