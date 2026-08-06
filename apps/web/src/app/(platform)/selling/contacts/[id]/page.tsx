import { redirect } from "next/navigation";

export default async function SellingContactDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/selling/contacts/${id}/overview`);
}
