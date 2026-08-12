import Link from "next/link";
import { ReactNode } from "react";

interface MetricCardProps {
  title: string;
  value: string | number;
  subtitle?: string;
  icon: string;
  iconBgColor?: string;
  iconColor?: string;
  valueColor?: string;
  subtitleColor?: string;
  formatAsCurrency?: boolean;
  href?: string;
}

export function MetricCard({
  title,
  value,
  subtitle,
  icon,
  iconBgColor = "bg-blue-50",
  iconColor = "text-blue-500",
  valueColor = "text-blue-600",
  subtitleColor,
  formatAsCurrency = true,
  href,
}: MetricCardProps) {
  const displayValue =
    typeof value === "number" && formatAsCurrency
      ? `$${value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
      : value;

  const content = (
    <div className="bg-white border border-gray-200 rounded-xl p-5 hover:shadow-sm transition-shadow">
      <div className="flex items-start justify-between gap-3">
        <p className="text-sm font-semibold text-gray-900">{title}</p>
        <div
          className={`w-10 h-10 shrink-0 rounded-xl ${iconBgColor} flex items-center justify-center ${iconColor}`}
        >
          <i className={`${icon} text-xl`} />
        </div>
      </div>
      <div className="mt-5">
        {subtitle && (
          <p className={`text-xs font-medium ${subtitleColor ?? valueColor}`}>
            {subtitle}
          </p>
        )}
        <p className={`text-[28px] font-bold leading-tight mt-1 ${valueColor}`}>
          {displayValue}
        </p>
      </div>
    </div>
  );

  if (href) return <Link href={href}>{content}</Link>;
  return content;
}
