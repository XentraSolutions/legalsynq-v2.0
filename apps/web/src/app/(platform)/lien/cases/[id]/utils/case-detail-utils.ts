import type { SettlementHistoryItemV3 } from "@/lib/settlement/settlement.types";
import { formatLegacyDateOnly, formatLegacyShortTimestamp } from "@/lib/format-date";

export function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return "";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
}

export function describeSettlementHistoryItem(
  item: SettlementHistoryItemV3,
): string {
  let description: string;
  const lienReference = item.lienCode || item.lienId;
  switch (item.type) {
    case "payment":
      description = `Payment of ${formatCurrency(item.amount)}${item.payee ? ` to ${item.payee}` : ""}${item.checkNumber ? ` (Check #${item.checkNumber})` : ""}`;
      break;
    case "reduction":
      description = `Reduction of ${formatCurrency(item.amount)}`;
      break;
    case "settlement":
      description = `Settlement of ${formatCurrency(item.amount)}${item.status ? ` — ${item.status}` : ""}`;
      break;
  }
  description += ` to lien ${lienReference}`;
  return item.note ? `${description}: ${item.note}` : description;
}

export function formatNoteDate(iso: string, timezone: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "";
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHrs = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return "Just now";
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHrs < 24) return `${diffHrs}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;

  return formatLegacyDateOnly(iso, timezone);
}

export function formatNoteTimestamp(iso: string, timezone: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "";
  return formatLegacyShortTimestamp(iso, timezone);
}

export function getInitials(name: string): string {
  return name
    .split(" ")
    .map((w) => w[0])
    .join("")
    .toUpperCase()
    .slice(0, 2);
}

const AVATAR_COLORS = [
  "bg-blue-100 text-blue-700",
  "bg-emerald-100 text-emerald-700",
  "bg-purple-100 text-purple-700",
  "bg-amber-100 text-amber-700",
  "bg-rose-100 text-rose-700",
  "bg-cyan-100 text-cyan-700",
];

export function avatarColor(name: string): string {
  let hash = 0;
  for (let i = 0; i < name.length; i++)
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}
