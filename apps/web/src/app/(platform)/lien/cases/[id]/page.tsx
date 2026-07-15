import { redirect } from "next/navigation";

const VALID_TABS = [
  "details",
  "liens",
  "documents",
  "servicing",
  "notes",
  "taskmanager",
];

export default async function CaseDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { id } = await params;
  const resolvedSearchParams = await searchParams;
  const rawValue = resolvedSearchParams[""];

  // Flatten if it's an array, otherwise fallback to the details tab.
  // TEMP: legacy links used `?=<tab>` to deep-link a tab; honor it once
  // more while redirecting into the new nested routes.
  const requestedTab = Array.isArray(rawValue) ? rawValue[0] : rawValue;
  const tab =
    requestedTab && VALID_TABS.includes(requestedTab) ? requestedTab : "details";

  redirect(`/lien/cases/${id}/${tab}`);
}
