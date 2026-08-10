import { XeniaAssistant } from '@/components/xenia/xenia-assistant';

export const dynamic = 'force-dynamic';

export default function XeniaPage() {
  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="mb-4">
        <h1 className="text-xl font-bold text-[#0f1928]">Xenia</h1>
        <p className="mt-1 text-sm text-gray-500">
          Tenant-aware assistant for product context, drafting, and workflow support.
        </p>
      </div>
      <XeniaAssistant mode="page" />
    </div>
  );
}
