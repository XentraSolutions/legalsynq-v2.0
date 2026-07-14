import { CollapsibleSection } from "./collapsible-section";

export type ContactsSectionItem = {
  icon: string;
  iconBgClass: string;
  iconColorClass: string;
  name: string;
  role: string;
};

export function ContactsSection({ items }: { items: ContactsSectionItem[] }) {
  return (
    <CollapsibleSection title="Contacts" icon="ri-contacts-line">
      {/* TEMP: visual fallback data for UI review only */}
      <div className="space-y-2">
        {items.map((item, idx) => (
          <div
            key={idx}
            className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50"
          >
            <div
              className={`w-8 h-8 rounded-full ${item.iconBgClass} flex items-center justify-center shrink-0`}
            >
              <i className={`${item.icon} text-sm ${item.iconColorClass}`} />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">
                {item.name}
              </p>
              <p className="text-xs text-gray-400">{item.role}</p>
            </div>
          </div>
        ))}
      </div>
    </CollapsibleSection>
  );
}
