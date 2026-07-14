import { CollapsibleSection } from "./collapsible-section";

export function SmsSection() {
  return (
    <CollapsibleSection title="SMS" icon="ri-message-2-line">
      <div className="flex justify-center py-2">
        <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
          <i className="ri-message-2-line text-sm" />
          Send SMS
        </button>
      </div>
    </CollapsibleSection>
  );
}
