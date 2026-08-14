import type { ReactNode } from 'react';
import { ScrollView } from 'react-native';

interface CaseDetailTabPageProps {
  children: ReactNode;
  testID?: string;
}

export function CaseDetailTabPage({ children, testID }: CaseDetailTabPageProps) {
  return (
    <ScrollView
      className="flex-1"
      contentContainerClassName="px-6 pb-10 pt-6"
      contentContainerStyle={{ flexGrow: 1 }}
      keyboardShouldPersistTaps="handled"
      testID={testID}
    >
      {children}
    </ScrollView>
  );
}
