"use client";

import { useMemo, useTransition } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { formatFundingNumber } from "@/lib/synqlien-funding-portal/format";
import {
  buildOfferedLiensPageSizeHref,
  getOfferedLiensPageSizeOptions,
} from "@/lib/synqlien-funding-portal/pagination";

interface OfferedLiensPageSizeSelectProps {
  pageSize: number;
  firstItem: number;
  lastItem: number;
}

export function OfferedLiensPageSizeSelect({
  pageSize,
  firstItem,
  lastItem,
}: OfferedLiensPageSizeSelectProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [isPending, startTransition] = useTransition();
  const displayValue = `${formatFundingNumber(firstItem)}-${formatFundingNumber(lastItem)}`;

  const pageSizeOptions = useMemo(() => getOfferedLiensPageSizeOptions(pageSize), [pageSize]);

  function handlePageSizeChange(value: string) {
    const nextPageSize = Number.parseInt(value, 10);
    if (!Number.isFinite(nextPageSize) || nextPageSize <= 0 || nextPageSize === pageSize) return;

    const href = buildOfferedLiensPageSizeHref({
      pathname,
      searchParams: searchParams?.toString() ?? "",
      pageSize: nextPageSize,
    });
    startTransition(() => {
      router.push(href);
    });
  }

  return (
    <Select
      value={String(pageSize)}
      disabled={isPending}
      onValueChange={handlePageSizeChange}
    >
      <SelectTrigger
        aria-label="Working set"
        className="h-9 min-w-[92px] w-auto rounded-[8px] border-[#e5e5e5] px-3 text-[14px] font-normal leading-5 text-[#0a0a0a] shadow-[0_1px_1px_rgba(0,0,0,0.08)] focus:border-[#f4a076] focus:ring-[#fdf1eb]"
      >
        <SelectValue>{displayValue}</SelectValue>
      </SelectTrigger>
      <SelectContent className="min-w-[92px]">
        {pageSizeOptions.map(size => (
          <SelectItem key={size} value={String(size)}>
            1-{formatFundingNumber(size)}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
