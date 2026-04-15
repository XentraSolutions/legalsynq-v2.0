import { requireOrg } from '@/lib/auth-guards';
import { SchedulesListClient } from './schedules-list-client';

export default async function SchedulesPage() {
  await requireOrg();
  return <SchedulesListClient />;
}
