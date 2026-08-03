import {
  DEFAULT_MENU_VISIBILITY,
  MENU_VISIBILITY_HIERARCHY,
} from '@/shared/constants/menuSettings';

import { AppMenu, getVisibleAccountModes, getVisibleMenuSections } from './AppMenu';

describe('AppMenu', () => {
  it('exports the drawer component', () => {
    expect(typeof AppMenu).toBe('function');
  });

  it('filters menu children and empty sections using their visibility flags', () => {
    const defaultSections = getVisibleMenuSections('selling', DEFAULT_MENU_VISIBILITY);

    expect(defaultSections).toHaveLength(1);
    expect(defaultSections[0]?.children.map((child) => child.label)).toEqual(['Cases']);

    const sectionsWithReports = getVisibleMenuSections('selling', {
      ...DEFAULT_MENU_VISIBILITY,
      reports: true,
    });
    expect(sectionsWithReports.map((section) => section.label)).toEqual([
      'Management',
      'Tools & Utilities',
    ]);
  });

  it('filters account types as children of the account-type group', () => {
    expect(getVisibleAccountModes(DEFAULT_MENU_VISIBILITY)).toEqual(['buying']);
    expect(getVisibleAccountModes({ ...DEFAULT_MENU_VISIBILITY, selling: true })).toEqual([
      'selling',
      'buying',
    ]);
  });

  it('routes Liens to the all-liens list in both account modes', () => {
    const visibility = { ...DEFAULT_MENU_VISIBILITY, liens: true, selling: true };

    for (const mode of ['selling', 'buying'] as const) {
      const liensItem = getVisibleMenuSections(mode, visibility)
        .flatMap((section) => section.children)
        .find((child) => child.label === 'Liens');

      expect(liensItem?.route).toBe('MyLiens');
    }
  });

  it('defines the same parent-child hierarchy shown in settings', () => {
    expect(
      MENU_VISIBILITY_HIERARCHY.map((item) => ({
        label: item.label,
        children: 'children' in item ? item.children.map((child) => child.label) : [],
      }))
    ).toEqual([
      { label: 'Dashboard', children: [] },
      { label: 'Xenia AI', children: [] },
      { label: 'Account Type', children: ['Selling', 'Buying'] },
      {
        label: 'Management',
        children: ['Task Manager', 'Cases', 'Liens', 'Bill of Sales', 'Servicing', 'Contacts'],
      },
      {
        label: 'Tools & Utilities',
        children: ['Reports', 'Batch Upload', 'Document Handling', 'User Management'],
      },
      { label: 'Settings', children: [] },
    ]);
  });
});
