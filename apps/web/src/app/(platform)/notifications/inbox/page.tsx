import { NotificationInboxClient } from "./notification-inbox-client";

export const dynamic = "force-dynamic";

// Personal notification center. Backed by mock data for now — the real feed
// endpoint isn't ready yet (distinct from the tenant-admin email delivery
// dashboard at /notifications, which is a separate, already-shipped page).
export default function NotificationInboxPage() {
  return <NotificationInboxClient />;
}
