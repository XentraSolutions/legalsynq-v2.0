"use client";

import { useState } from "react";

type TabType = {
  label: string;
  key: string;
  badge?: number;
};
export interface TabsProps {
  tabs: TabType[];
  bordered: boolean;
  className?: string;
  defaultTab?: string;
  onChange?: (e: string) => void;
}

export function Tabs({
  bordered,
  className,
  tabs,
  defaultTab,
  onChange,
}: TabsProps) {
  const [activeTab, setActiveTab] = useState<string>(defaultTab ?? "");

  return (
    <div
      className={`flex items-center h-[38px] gap-1 bg-[#FAFAFA] rounded-md p-1 ${bordered ? "border-b border-gray-200" : ""} ${className}`}
    >
      {tabs.map((tab) => (
        <TabButton
          key={tab.key}
          bordered={bordered}
          active={tab.key === activeTab}
          onClick={() => {
            setActiveTab(tab.key);
            onChange?.(tab.key);
          }}
          badge={tab.badge}
        >
          {tab.label}
        </TabButton>
      ))}
    </div>
  );
}

function TabButton({
  active,
  bordered,
  onClick,
  children,
  badge,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
  bordered: boolean;
  badge?: number;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={
        "h-[30px] px-3 text-sm font-medium transition-colors rounded-md flex-1 cursor-pointer " +
        (active
          ? bordered
            ? "border-b-2 border-[#EE7132] text-[#EE7132] "
            : "bg-[#EE7132] border border-[#F4A076] shadow-sm text-white"
          : "border border-transparent text-gray-500 hover:text-gray-700")
      }
      aria-current={active && bordered ? "page" : undefined}
    >
      {children}
      {!!badge && (
        <span className="ml-1.5 inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 text-[10px] font-semibold rounded-full bg-primary/10 text-primary">
          {badge}
        </span>
      )}
    </button>
  );
}
