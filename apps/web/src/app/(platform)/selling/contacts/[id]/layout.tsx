import { ContactDetailShell } from "@/components/lien/contact-detail/shell";

const PRIMARY_BUTTON_CLASSNAME =
  "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

export default async function SellingContactDetailLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return (
    <ContactDetailShell
      id={id}
      basePath="/selling/contacts"
      primaryButtonClassName={PRIMARY_BUTTON_CLASSNAME}
    >
      {children}
    </ContactDetailShell>
  );
}
