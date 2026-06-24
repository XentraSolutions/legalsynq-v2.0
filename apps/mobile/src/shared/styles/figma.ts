export const FIGMA_COLORS = {
  accent: '#f97332',
  appBackground: '#f7f7f8',
  appBackgroundDark: '#050506',
  card: '#ffffff',
  cardDark: '#191a1f',
  drawerDark: '#18191d',
  muted: '#8f929b',
  shadowDark: '#000000',
  shadowLight: '#d7d9de',
} as const;

export const FIGMA_TEXT = {
  dashboardGreeting: 'font-jakarta-bold text-[13px] leading-[16px]',
  dashboardSubtitle: 'font-jakarta-medium text-[10px] leading-[13px]',
  dateLabel: 'font-jakarta-semibold text-[12px] leading-[16px]',
  statLabel: 'font-jakarta-medium text-[10px] leading-[13px]',
  statValue: 'font-jakarta-bold text-[15px] leading-[19px]',
  microStrong: 'font-jakarta-bold text-[10px] leading-[13px]',
  microMeta: 'font-jakarta-semibold text-[10px] leading-[13px]',
  cardTitle: 'font-jakarta-bold text-[15px] leading-[19px]',
  cardDescription: 'font-jakarta-medium text-[11px] leading-[15px]',
  rowLabel: 'font-jakarta-bold text-[12px] leading-[16px]',
  rowValue: 'font-jakarta-semibold text-[12px] leading-[16px]',
  rowMuted: 'font-jakarta-medium text-[12px] leading-[16px]',
  rowMeta: 'font-jakarta-semibold text-[11px] leading-[15px]',
  cta: 'font-jakarta-bold text-[13px] leading-[17px]',
  donutValue: 'font-jakarta-bold text-[21px] leading-[25px]',
  donutCaption: 'font-jakarta-semibold text-[10px] leading-[13px]',
  screenTitle: 'font-jakarta-bold text-[17px] leading-[22px]',
  screenSubtitle: 'font-jakarta-medium text-[12px] leading-[16px]',
  sectionTitle: 'font-jakarta-semibold text-[20px] leading-[26px]',
  body: 'font-jakarta-medium text-[14px] leading-[20px]',
  bodyStrong: 'font-jakarta-semibold text-[14px] leading-[20px]',
  formLabel: 'font-jakarta-medium text-[12px] leading-[16px]',
  input: 'font-jakarta-medium text-[14px] leading-[20px]',
  drawerName: 'font-jakarta-semibold text-[27px] leading-[33px]',
  drawerRole: 'font-jakarta-medium text-[22px] leading-[28px]',
  drawerHeading: 'font-jakarta-semibold text-[28px] leading-[34px]',
  drawerItem: 'font-jakarta-semibold text-[27px] leading-[33px]',
  drawerChoice: 'font-jakarta-medium text-[27px] leading-[33px]',
  avatarInitials: 'font-jakarta-bold text-[18px] leading-[24px]',
} as const;

export function cx(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(' ');
}
