import { Image, Text, View } from 'react-native';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';

import splashInvertImage from '@/assets/images/splash-invert.png';
import splashImage from '@/assets/images/splash.png';
import { cx, FIGMA_TEXT } from '@/shared/styles';

export function AuthHeader({ title, subtitle }: { title: string; subtitle: string }) {
  const { colorScheme } = useNativeWindColorScheme();
  const logoSource = colorScheme === 'light' ? splashInvertImage : splashImage;

  return (
    <View>
      <Image
        accessibilityLabel="LegalSynq"
        className="mx-auto h-10 w-[140px]"
        resizeMode="contain"
        source={logoSource}
      />
      <Text className="mt-8 font-jakarta-bold text-[28px] leading-[34px] text-[#202228] dark:text-white">
        {title}
      </Text>
      <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>
        {subtitle}
      </Text>
    </View>
  );
}
