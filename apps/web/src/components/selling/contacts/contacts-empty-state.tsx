import { Button } from "@/components/ui/button";

const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

export function ContactsEmptyState({
  icon,
  title,
  description,
  actionLabel,
  onAction,
}: {
  icon: string;
  title: string;
  description: string;
  actionLabel?: string;
  onAction?: () => void;
}) {
  return (
    <div className="flex flex-col items-center justify-center text-center py-16 px-6">
      <div className="w-14 h-14 rounded-2xl bg-gray-100 flex items-center justify-center mb-5">
        <i className={`${icon} text-2xl text-gray-500`} />
      </div>
      <h3 className="text-2xl font-bold text-gray-900">{title}</h3>
      <p className="text-sm text-gray-400 mt-2 max-w-sm">{description}</p>
      {actionLabel && onAction && (
        <Button
          className={`mt-6 ${PRIMARY_BUTTON_CLASSNAME}`}
          iconDivider
          rightIcon={<i className="ri-add-line text-base" />}
          onClick={onAction}
        >
          {actionLabel}
        </Button>
      )}
    </div>
  );
}
