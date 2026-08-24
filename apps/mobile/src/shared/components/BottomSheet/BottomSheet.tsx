import type { ReactNode } from 'react';
import { forwardRef } from 'react';
import GorhomBottomSheet, { BottomSheetView } from '@gorhom/bottom-sheet';
import { Text, View } from 'react-native';

export interface BottomSheetProps {
  snapPoints?: Array<string | number>;
  children: ReactNode;
  title?: string;
  index?: number;
  onChange?: (index: number) => void;
}

export const BottomSheet = forwardRef<GorhomBottomSheet, BottomSheetProps>(
  ({ snapPoints = ['50%', '90%'], children, title, index = -1, onChange }, ref) => (
    <GorhomBottomSheet ref={ref} index={index} snapPoints={snapPoints} onChange={onChange}>
      <BottomSheetView className="px-5 pb-6">
        <View className="mx-auto mb-4 h-1 w-8 rounded-full bg-border" />
        {title ? <Text className="mb-4 text-xl font-semibold text-content-primary">{title}</Text> : null}
        {children}
      </BottomSheetView>
    </GorhomBottomSheet>
  )
);

BottomSheet.displayName = 'BottomSheet';
