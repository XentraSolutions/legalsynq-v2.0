import type { LucideIcon } from "lucide-react";
import { Button } from "@/components/selling/button";

export function ContactsEmptyState({
  icon: Icon,
  title,
  description,
  actionLabel,
  onAction,
}: {
  icon: LucideIcon;
  title: string;
  description: string;
  actionLabel?: string;
  onAction?: () => void;
}) {
  return (
    <div className="flex flex-col items-center justify-center text-center py-16 px-6">
      <div className="w-14 h-14 rounded-2xl bg-gray-100 flex items-center justify-center mb-5">
        <Icon className="text-2xl text-gray-500 h-8 w-8" />
      </div>
      <h3 className="text-2xl font-bold text-gray-900">{title}</h3>
      <p className="text-sm text-gray-400 mt-2 max-w-sm">{description}</p>
      {actionLabel && onAction && (
        <Button
          variant="primary"
          className="mt-6"
          rightIcon="plus"
          onClick={onAction}
        >
          {actionLabel}
        </Button>
      )}
    </div>
  );
}
