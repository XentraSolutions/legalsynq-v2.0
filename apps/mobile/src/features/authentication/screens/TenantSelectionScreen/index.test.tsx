import { fireEvent, render, waitFor } from '@testing-library/react-native';

import { TenantSelectionScreen } from './index';

const mockNavigate = jest.fn();
const mockRemoveRememberedTenant = jest.fn();
const mockGetRememberedTenants = jest.fn();
const mockGetActiveTenant = jest.fn();

jest.mock('@react-navigation/native', () => ({
  useFocusEffect: (callback: () => void) => callback(),
  useNavigation: () => ({ navigate: mockNavigate }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({
    showError: jest.fn(),
    showSuccess: jest.fn(),
    showWarning: jest.fn(),
  }),
}));

jest.mock('@/shared/services/Authentication', () => ({
  AuthenticationService: { clearSession: jest.fn() },
}));

jest.mock('@/shared/services/TenantSelection', () => ({
  TenantSelectionService: {
    addLocalTenantCode: jest.fn(),
    getActiveTenant: (...args: unknown[]) => mockGetActiveTenant(...args),
    getRememberedTenants: (...args: unknown[]) => mockGetRememberedTenants(...args),
    removeRememberedTenant: (...args: unknown[]) => mockRemoveRememberedTenant(...args),
    setActiveTenant: jest.fn(),
  },
}));

const activeTenant = {
  id: 'tenant-1',
  isConfirmed: true,
  tenantCode: 'alpha',
  tenantName: 'Alpha Legal',
};

const removableTenant = {
  id: 'tenant-2',
  isConfirmed: true,
  tenantCode: 'beta',
  tenantName: 'Beta Legal',
};

describe('TenantSelectionScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetRememberedTenants.mockResolvedValue([activeTenant, removableTenant]);
    mockGetActiveTenant.mockResolvedValue(activeTenant);
    mockRemoveRememberedTenant.mockResolvedValue(true);
  });

  it('requires confirmation before removing a remembered tenant code', async () => {
    const { getByLabelText, getByText, queryByText } = render(<TenantSelectionScreen />);

    await waitFor(() => expect(getByLabelText('Remove beta')).toBeTruthy());
    fireEvent.press(getByLabelText('Remove beta'));

    expect(getByText('Remove tenant code?')).toBeTruthy();
    expect(getByText(/You will need to enter this code again/)).toBeTruthy();
    expect(mockRemoveRememberedTenant).not.toHaveBeenCalled();

    fireEvent.press(getByText('Cancel'));
    expect(queryByText('Remove tenant code?')).toBeNull();
    expect(mockRemoveRememberedTenant).not.toHaveBeenCalled();

    fireEvent.press(getByLabelText('Remove beta'));
    fireEvent.press(getByText('Remove'));

    await waitFor(() => {
      expect(mockRemoveRememberedTenant).toHaveBeenCalledWith('tenant-2');
    });
  });
});
