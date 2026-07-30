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
      className={`flex flex-wrap gap-4 bg-gray-200 rounded-md p-1 ${bordered ? "border-b border-gray-200" : ""} ${className}`}
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
        "p-1 text-sm font-medium transition-colors rounded-md flex-1 cursor-pointer " +
        (active
          ? bordered
            ? "border-b-2 border-indigo-600 text-indigo-600"
            : "bg-white "
          : "border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300")
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
