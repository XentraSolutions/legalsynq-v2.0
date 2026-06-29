import { LegacyContactDetailClient } from './legacy-contact-detail-client';

export default async function LegacyContactDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <LegacyContactDetailClient id={id} />;
}
