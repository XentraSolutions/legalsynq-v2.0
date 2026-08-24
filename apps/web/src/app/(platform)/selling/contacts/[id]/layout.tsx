import { CompanyDetailShell } from "@/components/selling/contacts/company-detail-shell";

export default async function SellingContactDetailLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <CompanyDetailShell id={id}>{children}</CompanyDetailShell>;
}
