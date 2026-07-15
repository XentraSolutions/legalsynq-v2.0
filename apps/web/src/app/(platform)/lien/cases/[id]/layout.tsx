import { CaseDetailShell } from "./case-detail-shell";

export default async function CaseDetailLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <CaseDetailShell id={id}>{children}</CaseDetailShell>;
}
