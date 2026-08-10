"use client";

import { useEffect, useState } from "react";
import { CaseTaskManager } from "@/components/lien/case-task-manager";
import { useCaseWorkflows } from "@/hooks/use-case-workflows";
import { workflowApi, type WorkflowInstanceDetail } from "@/lib/workflow";
import type { CaseDetail } from "@/lib/cases";

export function TaskManagerTab({ caseDetail }: { caseDetail: CaseDetail }) {
  const { active } = useCaseWorkflows(caseDetail.id);
  const [workflowDetail, setWorkflowDetail] =
    useState<WorkflowInstanceDetail | null>(null);

  useEffect(() => {
    const instanceId = active?.workflowInstanceId;
    if (!instanceId) {
      setWorkflowDetail(null);
      return;
    }
    workflowApi
      .getDetail(caseDetail.id, instanceId)
      .then((res) => setWorkflowDetail(res.data ?? null))
      .catch(() => setWorkflowDetail(null));
  }, [caseDetail.id, active?.workflowInstanceId]);

  return (
    <CaseTaskManager
      caseId={caseDetail.id}
      workflowStageId={workflowDetail?.currentStageId ?? undefined}
    />
  );
}
