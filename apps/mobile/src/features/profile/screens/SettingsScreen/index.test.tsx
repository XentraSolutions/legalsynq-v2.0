import { render } from '@testing-library/react-native';

import { SettingsScreen } from './index';

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ goBack: jest.fn() }),
}));

jest.mock('@/shared/hooks/useApiMode', () => ({
  useApiMode: () => ({ mode: 'current', setMode: jest.fn() }),
}));

jest.mock('@/shared/hooks/useDashboardSettings', () => ({
  useDashboardSettings: () => ({
    settings: { useDummyData: false },
    setUseDummyData: jest.fn(),
  }),
}));

jest.mock('@/shared/hooks/useMenuSettings', () => ({
  useMenuSettings: () => ({
    settings: {},
    setMenuGroupVisible: jest.fn(),
    setMenuVisible: jest.fn(),
  }),
}));

describe('SettingsScreen', () => {
  const originalEnvironment = process.env.EXPO_PUBLIC_APP_ENV;

  afterEach(() => {
    process.env.EXPO_PUBLIC_APP_ENV = originalEnvironment;
  });

  it('exports the screen entrypoint', () => {
    expect(typeof SettingsScreen).toBe('function');
  });

  it('hides Menu Visibility and Reports settings in production', () => {
    process.env.EXPO_PUBLIC_APP_ENV = 'production';

    const { queryByText } = render(<SettingsScreen />);

    expect(queryByText('Menu Visibility')).toBeNull();
    expect(queryByText('Reports')).toBeNull();
    expect(queryByText('Use Dummy Dashboard Data')).toBeNull();
  });

  it('shows Menu Visibility and Reports settings outside production', () => {
    process.env.EXPO_PUBLIC_APP_ENV = 'qa';

    const { getAllByText, getByText } = render(<SettingsScreen />);

    expect(getByText('Menu Visibility')).toBeTruthy();
    expect(getAllByText('Reports')).not.toHaveLength(0);
    expect(getByText('Use Dummy Dashboard Data')).toBeTruthy();
  });
});
