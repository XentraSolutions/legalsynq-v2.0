import { Chip, type ChipProps } from "@/components/ui/chip";

interface LienStatusBadgeProps {
  status: string;
}

const STATUS_COLOR: Record<string, NonNullable<ChipProps["color"]>> = {
  Draft: "default",
  Offered: "info",
  Sold: "success",
  Withdrawn: "danger",
  Pending: "warning",
};

export function LienStatusBadge({ status }: LienStatusBadgeProps) {
  const color = STATUS_COLOR[status] ?? "default";

  return (
    <Chip variant="soft" size="lg" color={color}>
      {status}
    </Chip>
  );
}
