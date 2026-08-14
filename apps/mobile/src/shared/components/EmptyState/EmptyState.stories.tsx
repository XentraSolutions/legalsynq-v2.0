import { Ionicons } from '@expo/vector-icons';

import { EmptyState } from './EmptyState';

export default { title: 'Shared/EmptyState', component: EmptyState };

export function Default() {
  return (
    <EmptyState
      actionLabel="Browse Market"
      description="Try another filter or search term."
      icon={<Ionicons color="#94a3b8" name="search" size={64} />}
      title="No liens found"
      onAction={() => undefined}
    />
  );
}
