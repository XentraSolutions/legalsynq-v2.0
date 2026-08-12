'use client';

import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
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
  /** Remix icon class name (e.g. "ri-eye-line") or a lucide-react icon component. */
  icon?: string | LucideIcon;
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
  /** Custom trigger element (e.g. a labeled Button); defaults to a bare ellipsis icon button. */
  trigger?: ReactNode;
  align?: 'start' | 'end';
}

export function ActionMenu({ items, triggerIcon = 'ri-more-2-fill', trigger, align = 'end' }: ActionMenuProps) {
  return (
    <DropdownMenu>
      {trigger ? (
        <DropdownMenuTrigger asChild onClick={(e) => e.stopPropagation()}>
          {trigger}
        </DropdownMenuTrigger>
      ) : (
        <DropdownMenuTrigger
          onClick={(e) => e.stopPropagation()}
          aria-label="Actions menu"
          className="p-1.5 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors outline-none"
        >
          <i className={`${triggerIcon} text-base`} />
        </DropdownMenuTrigger>
      )}
      <DropdownMenuContent align={align} className="w-48">
        {items.map((item, i) => {
          const hasReason = Boolean(item.disabled && item.disabledReason);
          return (
            <div key={i}>
              {item.divider && i > 0 && <DropdownMenuSeparator />}
              <DropdownMenuItem
                disabled={item.disabled}
                onClick={(e) => { e.stopPropagation(); item.onClick(); }}
                className={cn(
                  'flex gap-2',
                  hasReason ? 'items-start' : 'items-center',
                  item.variant === 'danger' && 'text-red-600 focus:bg-red-50'
                )}
              >
                {item.icon &&
                  (typeof item.icon === 'string' ? (
                    <i className={cn(`${item.icon} text-base shrink-0`, hasReason && 'mt-px')} />
                  ) : (
                    <item.icon className={cn('h-4 w-4 shrink-0', hasReason && 'mt-px')} />
                  ))}
                <span className="flex flex-col gap-0.5">
                  <span>{item.label}</span>
                  {/* LS-ID-TNT-015-004: Inline reason hint for disabled items */}
                  {hasReason && (
                    <span className="text-xs text-gray-400 font-normal leading-tight">
                      {item.disabledReason}
                    </span>
                  )}
                </span>
              </DropdownMenuItem>
            </div>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
