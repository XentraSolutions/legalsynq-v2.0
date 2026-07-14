import { CollapsibleSection } from "./collapsible-section";

export function EmailSection() {
  return (
    <CollapsibleSection title="Email" icon="ri-mail-send-line">
      <div className="flex justify-center py-2">
        <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
          <i className="ri-mail-send-line text-sm" />
          Compose New Email
        </button>
      </div>
    </CollapsibleSection>
  );
}
