import { TaskPanel } from "@/components/lien/task-panel";
import { CollapsibleSection } from "../../../components/collapsible-section";

export function TasksSection({ caseId }: { caseId: string }) {
  return (
    <CollapsibleSection title="Tasks" icon="ri-task-line">
      <TaskPanel caseId={caseId} />
    </CollapsibleSection>
  );
}
