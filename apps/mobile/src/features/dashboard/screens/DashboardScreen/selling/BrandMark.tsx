import { View } from 'react-native';
import Svg, { Circle, Path } from 'react-native-svg';

export function BrandMark({ variant }: { variant: string }) {
  return (
    <View className="h-9 w-9 items-center justify-center rounded-xl bg-[#ede9ff] dark:bg-[#292344]">
      {variant === 'bars' ? (
        <View className="flex-row items-end gap-1">
          {[12, 21, 28].map((height) => (
            <View className="w-1.5 rounded-full bg-[#6254ff]" key={height} style={{ height }} />
          ))}
        </View>
      ) : variant === 'cube' ? (
        <View className="h-6 w-6 rotate-45 rounded-[5px] bg-[#3b82f6]" />
      ) : variant === 'wave' ? (
        <View className="flex-row items-center gap-0.5">
          {[22, 18, 24].map((height) => (
            <View
              className="w-2 rotate-[-30deg] rounded-full bg-[#6254ff]"
              key={height}
              style={{ height }}
            />
          ))}
        </View>
      ) : variant === 'v' ? (
        <View className="h-7 w-6 flex-row items-end gap-1">
          <View className="h-6 w-2 rounded-t-full bg-[#6254ff]" />
          <View className="h-4 w-2 rounded-t-full bg-[#6254ff]" />
        </View>
      ) : (
        <Svg height={26} width={26} viewBox="0 0 26 26">
          <Circle cx="13" cy="13" fill="#6254ff" r="12" />
          <Path d="M13 1 A12 12 0 0 1 25 13 L13 13 Z" fill="#ffffff" opacity="0.9" />
          <Path d="M13 13 L4 22 A12 12 0 0 1 1 13 Z" fill="#ffffff" opacity="0.7" />
        </Svg>
      )}
    </View>
  );
}
