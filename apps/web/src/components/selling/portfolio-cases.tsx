"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Search } from "lucide-react";
import { toast } from "sonner";
import { PageHeader } from "@/components/lien/page-header";
import { Card } from "../ui/dashboard-card";
import { MetricCard } from "./dashboard/metric-card";
import { Tabs } from "./tabs";
import { Button } from "@/components/selling/button";
import { CasesTable } from "./cases-table";

export default function PortfolioCasesClient() {
  const router = useRouter();
  const [searchInput, setSearchInput] = useState("");

  return (
    <div className="space-y-4">
      <PageHeader
        title="Portfolio"
        subtitle="Manage, monitor, and bundle multiple liens into structured portfolios for sale."
        card={false}
      />
      <Tabs
        bordered={false}
        defaultTab="cases"
        tabs={[
          { key: "cases", label: "Cases" },
          { key: "liens", label: "Liens" },
        ]}
        onChange={(tab) => {
          if (tab === "liens") router.push("/selling/portfolio/lien");
        }}
      />
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <MetricCard
          label="Total Portfolio Value"
          value={0}
          description=""
          formatAsCurrency={true}
        />
        <MetricCard
          label="Total Pending"
          value={0}
          description=""
          formatAsCurrency={true}
        />
        <MetricCard
          label="Total Internal"
          value={0}
          description=""
          formatAsCurrency={true}
        />
        <MetricCard
          label="Total Sold"
          value={0}
          description=""
          formatAsCurrency={true}
        />
      </div>

      <Card title="All Cases">
        <div className="bg-white rounded-xl py-3 flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <div className="relative flex-1 min-w-[300px] max-w-md">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
              <input
                type="text"
                placeholder="Search"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              />
            </div>
            <Button
              variant="secondary"
              className="border-gray-300"
              leftIcon="settings2"
              onClick={() => toast.info("Case filters are coming soon.")}
            >
              Filter
            </Button>
          </div>
          <Button
            variant="primary"
            onClick={() => router.push("/selling/portfolio/cases/add")}
          >
            Add New Case
          </Button>
        </div>

        <CasesTable />
      </Card>
    </div>
  );
}
