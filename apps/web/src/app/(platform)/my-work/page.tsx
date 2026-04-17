import { requireOrg } from '@/lib/auth-guards';
import { MyWorkClient } from '@/components/my-work/my-work-client';

/**
 * LS-FLOW-E11.6 — tenant-portal "My Work" inbox.
 *
 * The page itself is a server component that re-asserts the org guard
 * (the platform layout already does so; we keep the call here as a
 * defence-in-depth pattern consistent with the other (platform) pages).
 *
 * All interactivity lives in <MyWorkClient/> which talks to Flow via
 * the BFF proxy /api/flow/* (see app/api/flow/[...path]/route.ts).
 *
 * No userId is sent on the wire — Flow's MyTasksController resolves
 * the calling user from the auth context, so tenant + user isolation
 * is enforced by the backend regardless of any request the client
 * crafts.
 */
export default async function MyWorkPage() {
  await requireOrg();

  return (
    <div className="p-6">
      <div className="max-w-4xl mx-auto">
        <MyWorkClient />
      </div>
    </div>
  );
}
