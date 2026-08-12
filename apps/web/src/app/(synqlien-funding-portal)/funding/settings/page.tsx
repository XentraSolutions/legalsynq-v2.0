import { requireOrg } from "@/lib/auth-guards";
import { ChangePasswordForm } from "./change-password-form";

export const dynamic = "force-dynamic";

export default async function SynqLienFundingSettingsPage() {
  const session = await requireOrg();
  const orgName = session.orgName || "Funding portal";
  const initials = buildInitials(orgName, session.email);

  return (
    <div className="w-full space-y-6">
      <div className="max-w-[760px]">
        <h1 className="text-[28px] font-semibold leading-9 tracking-normal text-[#0a0a0a]">
          Account Settings
        </h1>
        <p className="mt-1 text-[14px] font-normal leading-[1.6] text-[#737373]">
          Manage account security for your funding company portal.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(240px,320px)_minmax(0,640px)]">
        <section className="self-start rounded-[16px] border border-[#e5e5e5] bg-white p-5 shadow-[0_1px_1.5px_rgba(0,0,0,0.08)]">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#fdf1eb] text-center text-[18px] font-medium leading-[1.6] text-[#a95024]">
              {initials}
            </div>
            <div className="min-w-0">
              <p className="truncate text-[14px] font-bold leading-[1.6] text-[#0a0a0a]">
                {orgName}
              </p>
              <p className="text-[12px] font-normal leading-[1.6] text-[#737373]">
                Funding Company
              </p>
            </div>
          </div>

          <div className="mt-5 border-t border-[#f0f0f0] pt-4">
            <div>
              <p className="text-[12px] font-normal leading-[1.6] text-[#737373]">
                Email
              </p>
              <p className="mt-1 break-words text-[14px] font-medium leading-[1.6] text-[#0a0a0a]">
                {session.email}
              </p>
            </div>
          </div>
        </section>

        <section className="rounded-[16px] border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.08)]">
          <div className="mb-6 flex items-start gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#f5f5f5] text-[#737373]">
              <i className="ri-lock-password-line text-[18px]" aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <h2 className="text-[18px] font-bold leading-[1.6] text-[#0a0a0a]">
                Change password
              </h2>
              <p className="mt-0.5 text-[14px] font-normal leading-[1.6] text-[#737373]">
                Use a strong, unique password. Changes take effect immediately.
              </p>
            </div>
          </div>
          <ChangePasswordForm />
        </section>
      </div>
    </div>
  );
}

function buildInitials(orgName: string, email: string): string {
  const orgParts = orgName
    .split(/\s+/)
    .map(part => part.trim())
    .filter(Boolean);
  if (orgParts.length >= 2) {
    return `${orgParts[0][0]}${orgParts[1][0]}`.toUpperCase();
  }
  if (orgParts.length === 1) {
    return orgParts[0].slice(0, 2).toUpperCase();
  }

  const local = email.split("@")[0] ?? "";
  const parts = local.split(/[._-]/).filter(Boolean);
  if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  return (local.slice(0, 2) || "SL").toUpperCase();
}
