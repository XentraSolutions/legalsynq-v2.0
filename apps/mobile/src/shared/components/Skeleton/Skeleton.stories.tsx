import { View } from 'react-native';

import { Skeleton } from './Skeleton';

export default { title: 'Shared/Skeleton', component: Skeleton };

export function Variants() {
  return (
    <View className="gap-4 bg-white p-4">
      <Skeleton height={20} variant="text" width="70%" />
      <Skeleton height={80} width="100%" />
      <Skeleton height={48} variant="circle" width={48} />
    </View>
  );
}
