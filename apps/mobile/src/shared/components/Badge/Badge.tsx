import { Text, View } from 'react-native';

import { cx, FIGMA_TEXT } from '@/shared/styles';

export type BadgeVariant =
  | 'success'
  | 'warning'
  | 'error'
  | 'info'
  | 'neutral'
  | 'primary'
  | 'lien-available'
  | 'lien-pending'
  | 'lien-sold'
  | 'lien-settled'
  | 'lien-draft';

export interface BadgeProps {
  label: string;
  variant?: BadgeVariant;
}

const VARIANT_CLASSES: Record<BadgeVariant, { background: string; text: string }> = {
  success: { background: 'bg-success-100 dark:bg-[#133225]', text: 'text-success-700 dark:text-[#35d47d]' },
  warning: { background: 'bg-warning-100 dark:bg-[#3a301c]', text: 'text-warning-700 dark:text-[#f3c54f]' },
  error: { background: 'bg-error-100 dark:bg-[#3a1f24]', text: 'text-error-700 dark:text-[#ef5d62]' },
  info: { background: 'bg-info-100 dark:bg-[#16313a]', text: 'text-info-700 dark:text-[#67d7ee]' },
  primary: { background: 'bg-[#fde7d9] dark:bg-[#402513]', text: 'text-[#c9571b] dark:text-[#f97332]' },
  neutral: { background: 'bg-slate-100 dark:bg-[#2a2b30]', text: 'text-slate-700 dark:text-[#c7c8cc]' },
  'lien-available': { background: 'bg-success-100 dark:bg-[#133225]', text: 'text-success-700 dark:text-[#35d47d]' },
  'lien-pending': { background: 'bg-warning-100 dark:bg-[#3a301c]', text: 'text-warning-700 dark:text-[#f3c54f]' },
  'lien-sold': { background: 'bg-secondary-100 dark:bg-[#292344]', text: 'text-secondary-700 dark:text-[#a78bfa]' },
  'lien-settled': { background: 'bg-info-100 dark:bg-[#16313a]', text: 'text-info-700 dark:text-[#67d7ee]' },
  'lien-draft': { background: 'bg-slate-100 dark:bg-[#2a2b30]', text: 'text-slate-600 dark:text-[#a3a4ab]' },
};

export function Badge({ label, variant = 'neutral' }: BadgeProps) {
  const variantClasses = VARIANT_CLASSES[variant];

  return (
    <View className={cx('self-start rounded-full px-2.5 py-1', variantClasses.background)}>
      <Text className={cx(FIGMA_TEXT.microMeta, variantClasses.text)}>{label}</Text>
    </View>
  );
}
