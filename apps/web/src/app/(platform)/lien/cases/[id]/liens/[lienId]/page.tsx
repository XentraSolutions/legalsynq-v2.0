import { CaseLienDetailPageClient } from "./case-lien-detail-page-client";

export default async function CaseLienDetailPage({
  params,
}: {
  params: Promise<{ id: string; lienId: string }>;
}) {
  const { id, lienId } = await params;
  return <CaseLienDetailPageClient caseId={id} lienId={lienId} />;
}
