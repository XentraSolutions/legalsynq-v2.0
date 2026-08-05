export const MENU_VISIBILITY_OPTIONS = [
  { key: 'dashboard', label: 'Dashboard', defaultVisible: true },
  { key: 'xeniaAi', label: 'Xenia AI', defaultVisible: true },
  { key: 'buying', label: 'Buying', defaultVisible: true },
  { key: 'selling', label: 'Selling', defaultVisible: false },
  { key: 'taskManager', label: 'Task Manager', defaultVisible: false },
  { key: 'cases', label: 'Cases', defaultVisible: true },
  { key: 'liens', label: 'Liens', defaultVisible: true },
  { key: 'billOfSales', label: 'Bill of Sales', defaultVisible: false },
  { key: 'servicing', label: 'Servicing', defaultVisible: true },
  { key: 'contacts', label: 'Contacts', defaultVisible: false },
  { key: 'reports', label: 'Reports', defaultVisible: false },
  { key: 'batchUpload', label: 'Batch Upload', defaultVisible: false },
  { key: 'documentHandling', label: 'Document Handling', defaultVisible: false },
  { key: 'userManagement', label: 'User Management', defaultVisible: false },
  { key: 'settings', label: 'Settings', defaultVisible: true },
] as const;

export type MenuVisibilityKey = (typeof MENU_VISIBILITY_OPTIONS)[number]['key'];
export type MenuVisibilitySettings = Record<MenuVisibilityKey, boolean>;

export const MENU_VISIBILITY_HIERARCHY = [
  { key: 'dashboard', label: 'Dashboard' },
  { key: 'xeniaAi', label: 'Xenia AI' },
  {
    key: 'accountType',
    label: 'Account Type',
    children: [
      { key: 'selling', label: 'Selling' },
      { key: 'buying', label: 'Buying' },
    ],
  },
  {
    key: 'management',
    label: 'Management',
    children: [
      { key: 'taskManager', label: 'Task Manager' },
      { key: 'cases', label: 'Cases' },
      { key: 'liens', label: 'Liens' },
      { key: 'billOfSales', label: 'Bill of Sales' },
      { key: 'servicing', label: 'Servicing' },
      { key: 'contacts', label: 'Contacts' },
    ],
  },
  {
    key: 'tools',
    label: 'Tools & Utilities',
    children: [
      { key: 'reports', label: 'Reports' },
      { key: 'batchUpload', label: 'Batch Upload' },
      { key: 'documentHandling', label: 'Document Handling' },
      { key: 'userManagement', label: 'User Management' },
    ],
  },
  { key: 'settings', label: 'Settings' },
] as const satisfies ReadonlyArray<
  | { key: string; label: string }
  | {
      key: string;
      label: string;
      children: ReadonlyArray<{ key: MenuVisibilityKey; label: string }>;
    }
>;

export const MENU_SETTINGS_STORAGE_KEY = 'legalsynq.menu.settings';

export const DEFAULT_MENU_VISIBILITY = Object.fromEntries(
  MENU_VISIBILITY_OPTIONS.map(({ key, defaultVisible }) => [key, defaultVisible])
) as MenuVisibilitySettings;
