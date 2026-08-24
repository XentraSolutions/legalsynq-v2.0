"use client";

import { useCaseDetailContext } from "../case-detail-context";
import { TaskManagerTab } from "../tabs/task-manager/task-manager-tab";

export default function CaseTaskManagerPage() {
  const { d } = useCaseDetailContext();
  return <TaskManagerTab caseDetail={d} />;
}
