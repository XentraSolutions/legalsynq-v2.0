import { CaseDetailClient } from "./case-detail-client";

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

  // Flatten if it's an array, otherwise fallback to an empty string
  const servicing: string = Array.isArray(rawValue)
    ? rawValue[0]
    : (rawValue ?? "details");
  return <CaseDetailClient id={id} tab={servicing} />;
}
