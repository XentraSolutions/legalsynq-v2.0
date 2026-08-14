import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { CaseDetailTabPage } from '@/features/cases/components/CaseDetailTabPage';
import { cx, FIGMA_TEXT, SHADOWS } from '@/shared/styles';

interface CaseDetailPlaceholderPageProps {
  title: string;
}

export function CaseDetailPlaceholderPage({ title }: CaseDetailPlaceholderPageProps) {
  return (
    <CaseDetailTabPage testID={`case-${title.toLowerCase().replace(/\s+/g, '-')}-page`}>
      <View
        className="flex-1 items-center justify-center rounded-[20px] bg-white px-6 py-12 dark:bg-[#191a1f]"
        style={SHADOWS.sm}
      >
        <Ionicons color="#8f929b" name="documents-outline" size={38} />
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'mt-4 text-center text-[#202228] dark:text-white')}>
          {title}
        </Text>
        <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-center text-[#777a84] dark:text-[#a1a1aa]')}>
          This section is ready for case-specific content.
        </Text>
      </View>
    </CaseDetailTabPage>
  );
}
