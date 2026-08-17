import { Chip, type ChipProps } from "@/components/ui/chip";
import { STATUS_LABELS } from "./status-badge";

interface CaseStatusChipProps {
  status: string;
  label?: string;
}

const STATUS_COLOR: Record<string, NonNullable<ChipProps["color"]>> = {
  Draft: "default",
  Offered: "info",
  Sold: "success",
  Withdrawn: "danger",
  PreDemand: "success",
  "Pre-Demand": "success",
  DemandSent: "info",
  InNegotiation: "info",
  CaseSettled: "success",
  Closed: "danger",
  Pending: "warning",
  InProgress: "info",
  Completed: "success",
  Escalated: "danger",
  OnHold: "warning",
  Executed: "success",
  Cancelled: "danger",
  Processing: "info",
  Failed: "danger",
  Archived: "default",
  Active: "success",
  Inactive: "default",
  Invited: "info",
  Locked: "danger",
  Open: "success",
  Rejected: "danger",
  Litigation: "success",
  "Litigation (Open)": "success",
  "Litigation (Closed)": "success",
};

export function CaseStatusChip({ status, label }: CaseStatusChipProps) {
  const color = STATUS_COLOR[status] ?? "default";
  const resolvedLabel = label ?? STATUS_LABELS[status] ?? status;

  return (
    <Chip variant="soft" size="lg" color={color}>
      {resolvedLabel}
    </Chip>
  );
}
