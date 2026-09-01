import { Chip, type ChipProps } from "@/components/ui/chip";
import { STATUS_LABELS } from "./status-badge";

interface StatusChipProps {
  status: string;
  label?: string;
}

const STATUS_STYLES: Record<string, string> = {
  Closed: "bg-transparent     text-red-600    border-red-200",
  full_payment: "bg-transparent     text-[#10c469]    border-green-400",
  reduced_payment: "bg-transparent     text-[#10c469]    border-green-400",
  partial_loss: "bg-transparent     text-red-600      border-red-200",
  no_recovery: "bg-transparent     text-red-600      border-red-200",
};

export function SettlementStatusChip({ status, label }: StatusChipProps) {
  const style =
    STATUS_STYLES[status] ?? "bg-gray-50 text-gray-600 border-gray-200";
  const resolvedLabel = label ?? STATUS_LABELS[status] ?? status;

  return (
    <span
      className={`inline-flex items-center rounded-full border font-medium  px-2 py-0.5 text-xs ${style}`}
    >
      {resolvedLabel}
    </span>
  );
}
