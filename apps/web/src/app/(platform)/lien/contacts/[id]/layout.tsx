import { ContactDetailShell } from "./contact-detail-shell";

export default async function ContactDetailLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <ContactDetailShell id={id}>{children}</ContactDetailShell>;
}
