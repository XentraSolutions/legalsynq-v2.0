import { fireEvent, render } from '@testing-library/react-native';

import { CurrentTenantCard, getLoginDefaultValues, LoginScreen } from './index';

describe('LoginScreen', () => {
  it('exports the screen entrypoint', () => {
    expect(typeof LoginScreen).toBe('function');
  });

  it('does not prefill login credentials or tenant code in production', () => {
    expect(
      getLoginDefaultValues({
        isLegacyMode: false,
        isProduction: true,
      })
    ).toEqual({
      email: '',
      password: '',
      tenantCode: '',
    });
  });

  it('displays the current selected tenant name and code', () => {
    const onSwitch = jest.fn();
    const { getByText } = render(
      <CurrentTenantCard
        tenant={{
          id: 'tenant-1',
          isConfirmed: true,
          lastUsedAt: '2026-07-29T00:00:00Z',
          tenantCode: 'smith-law',
          tenantName: 'Smith Law Firm',
        }}
        onSwitch={onSwitch}
      />
    );

    expect(getByText('Current Tenant')).toBeTruthy();
    expect(getByText('Smith Law Firm (smith-law)')).toBeTruthy();

    fireEvent.press(getByText('Switch Tenant'));
    expect(onSwitch).toHaveBeenCalled();
  });
});
