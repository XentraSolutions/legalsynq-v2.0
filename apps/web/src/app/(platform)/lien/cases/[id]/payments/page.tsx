"use client";

import { useCaseDetailContext } from "../case-detail-context";
import { PaymentsTab } from "../tabs/payments/payments-tab";

export default function CasePaymentsPage() {
  const { id, canEdit } = useCaseDetailContext();
  return <PaymentsTab caseId={id} canEdit={canEdit} />;
}
