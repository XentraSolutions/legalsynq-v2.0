'use client';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';

export interface ActionMenuItem {
  label: string;
  icon?: string;
  onClick: () => void;
  variant?: 'default' | 'danger';
  disabled?: boolean;
  /** LS-ID-TNT-015-004: Short explanation shown below a disabled item. */
  disabledReason?: string;
  divider?: boolean;
}

interface ActionMenuProps {
  items: ActionMenuItem[];
  triggerIcon?: string;
}

export function ActionMenu({ items, triggerIcon = 'ri-more-2-fill' }: ActionMenuProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        onClick={(e) => e.stopPropagation()}
        aria-label="Actions menu"
        className="p-1.5 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors outline-none"
      >
        <i className={`${triggerIcon} text-base`} />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-48">
        {items.map((item, i) => (
          <div key={i}>
            {item.divider && i > 0 && <DropdownMenuSeparator />}
            <DropdownMenuItem
              disabled={item.disabled}
              onClick={(e) => { e.stopPropagation(); item.onClick(); }}
              className={cn(
                'flex items-start gap-2',
                item.variant === 'danger' && 'text-red-600 focus:bg-red-50'
              )}
            >
              {item.icon && <i className={`${item.icon} text-base mt-px shrink-0`} />}
              <span className="flex flex-col gap-0.5">
                <span>{item.label}</span>
                {/* LS-ID-TNT-015-004: Inline reason hint for disabled items */}
                {item.disabled && item.disabledReason && (
                  <span className="text-xs text-gray-400 font-normal leading-tight">
                    {item.disabledReason}
                  </span>
                )}
              </span>
            </DropdownMenuItem>
          </div>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
