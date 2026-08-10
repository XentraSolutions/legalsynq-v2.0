import { FundingNotificationsList } from "@/components/synqlien-funding-portal/funding-notifications";
import { getOfferedLiens } from "@/lib/synqlien-funding-portal";

export const dynamic = "force-dynamic";

export default async function FundingNotificationsPage() {
  const result = await getOfferedLiens({ page: 1, pageSize: 100, sort: "initialServiceDate", direction: "desc" });
  return <FundingNotificationsList rows={result.rows} />;
}
