import { redirect } from "next/navigation";
import { requireOrg } from "@/lib/auth-guards";
import { ProductRole } from "@/types";
import { lienServerApi } from "@/lib/lien-server-api";
import { ServerApiError } from "@/lib/server-api-client";
import { PortfolioTable } from "@/components/selling/portfolio-table";
import { liensService } from "@/lib/selling";
import { HydrationBoundary, dehydrate } from "@tanstack/react-query";
import { getQueryClient } from "@/lib/query-client";
import PortfolioClient from "@/components/selling/portfolio";

export const dynamic = "force-dynamic";
export default async function PortfolioPage() {
  const queryClient = getQueryClient();

  await queryClient.prefetchQuery({
    queryKey: ["portfolio"],
    queryFn: () => liensService.getSellingDashboard({}),
  });

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <PortfolioClient></PortfolioClient>
    </HydrationBoundary>
  );
}
