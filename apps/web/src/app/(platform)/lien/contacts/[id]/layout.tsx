import { ContactDetailShell } from "@/components/lien/contact-detail/shell";

export default async function ContactDetailLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return (
    <ContactDetailShell id={id} basePath="/lien/contacts">
      {children}
    </ContactDetailShell>
  );
}
