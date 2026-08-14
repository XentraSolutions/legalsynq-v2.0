import { View } from 'react-native';

import { Badge, type BadgeVariant } from './Badge';

export default { title: 'Shared/Badge', component: Badge };

const variants: BadgeVariant[] = [
  'success',
  'warning',
  'error',
  'info',
  'primary',
  'neutral',
  'lien-available',
  'lien-pending',
  'lien-sold',
  'lien-settled',
  'lien-draft',
];

export function Variants() {
  return (
    <View className="flex-row flex-wrap gap-2 bg-white p-4">
      {variants.map((variant) => (
        <Badge key={variant} label={variant} variant={variant} />
      ))}
    </View>
  );
}
