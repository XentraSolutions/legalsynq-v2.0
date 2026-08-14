import { fireEvent, render } from '@testing-library/react-native';

import { CaseServicingTab } from './index';

const caseItem = {
  caseManager: 'Aaron Law Group',
  caseNumber: '24-18743',
  clientDisplayName: 'Marcus Delgado',
  closedAtUtc: null,
  demandAmount: 10000,
  id: 'case-1',
  lawFirm: 'Aaron Law Group',
  settlementAmount: null,
  status: 'PreDemand',
} as Parameters<typeof CaseServicingTab>[0]['caseItem'];

const settlements = [
  {
    id: 'settlement-1',
    tenantId: 'tenant-1',
    caseId: 'case-1',
    lienId: 'lien-1',
    paymentNumber: 1,
    amount: 7500,
    status: 'Pending',
    createdAtUtc: '2026-05-01T00:00:00Z',
    updatedAtUtc: '2026-05-01T00:00:00Z',
  },
];

const payments = [
  {
    id: 'payment-1',
    tenantId: 'tenant-1',
    caseId: 'case-1',
    lienId: 'lien-1',
    paymentNumber: 2,
    amount: 5200,
    paymentDate: '2026-05-03',
    payee: 'Westside Orthopedic',
    checkNumber: '109215',
    createdAtUtc: '2026-05-03T00:00:00Z',
    updatedAtUtc: '2026-05-03T00:00:00Z',
  },
];

describe('CaseServicingTab', () => {
  it('renders Figma servicing details with case-backed values and empty fallbacks', () => {
    const onEdit = jest.fn();
    const screen = render(
      <CaseServicingTab
        caseItem={caseItem}
        payments={[]}
        reductions={[]}
        settlementError={false}
        settlementLoading={false}
        settlements={[]}
        updates={[]}
        onAddPayment={jest.fn()}
        onEdit={onEdit}
        onNoRecovery={jest.fn()}
        onSetupReduction={jest.fn()}
      />
    );

    expect(screen.getByTestId('case-servicing-page')).toBeTruthy();
    expect(screen.getByText('Servicing Details')).toBeTruthy();
    expect(screen.getByText('Pre-demand')).toBeTruthy();
    expect(screen.getByText('Current Law Firm')).toBeTruthy();
    expect(screen.getAllByText('Aaron Law Group')).toHaveLength(2);
    expect(screen.getAllByText('—')).toHaveLength(2);

    fireEvent.press(screen.getByLabelText('Edit servicing details'));
    expect(onEdit).toHaveBeenCalledTimes(1);
  });

  it('renders the settlement empty states and actions from the mobile design', () => {
    const onAddPayment = jest.fn();
    const onNoRecovery = jest.fn();
    const onSetupReduction = jest.fn();
    const screen = render(
      <CaseServicingTab
        caseItem={caseItem}
        payments={[]}
        reductions={[]}
        settlementError={false}
        settlementLoading={false}
        settlements={[]}
        updates={[]}
        onAddPayment={onAddPayment}
        onEdit={jest.fn()}
        onNoRecovery={onNoRecovery}
        onSetupReduction={onSetupReduction}
      />
    );

    fireEvent.press(screen.getByText('Settlement'));

    expect(screen.getByText('No Reduction')).toBeTruthy();
    expect(screen.getByText('Setup Reduction')).toBeTruthy();
    expect(screen.getByText('No Recovery')).toBeTruthy();
    expect(screen.getByText('Add Payment')).toBeTruthy();
    expect(screen.getByText('Open Liens')).toBeTruthy();
    expect(screen.getByText('No Open Liens Yet')).toBeTruthy();
    expect(screen.getByText('Closed Liens')).toBeTruthy();
    expect(screen.getByText('No Closed Liens Yet')).toBeTruthy();
    expect(screen.getByText('No Payment History Yet')).toBeTruthy();

    fireEvent.press(screen.getByText('Setup Reduction'));
    fireEvent.press(screen.getByText('No Recovery'));
    fireEvent.press(screen.getByText('Add Payment'));

    expect(onSetupReduction).toHaveBeenCalledTimes(1);
    expect(onNoRecovery).toHaveBeenCalledTimes(1);
    expect(onAddPayment).toHaveBeenCalledTimes(1);

    fireEvent.press(screen.getByLabelText('Collapse Settlement Details'));
    expect(screen.queryByText('No Reduction')).toBeNull();
    expect(screen.getByLabelText('Expand Settlement Details')).toBeTruthy();
  });

  it('switches between settlement and history content', () => {
    const screen = render(
      <CaseServicingTab
        caseItem={caseItem}
        payments={payments}
        reductions={[]}
        settlementError={false}
        settlementLoading={false}
        settlements={settlements}
        updates={[
          {
            id: 'update-1',
            title: 'Status updated',
            description: 'Case moved to Pre-demand',
            updatedAt: '2026-05-03T00:00:00Z',
          },
        ]}
        onAddPayment={jest.fn()}
        onEdit={jest.fn()}
        onNoRecovery={jest.fn()}
        onSetupReduction={jest.fn()}
      />
    );

    fireEvent.press(screen.getByText('Settlement'));
    expect(screen.getByText('Settlement Details')).toBeTruthy();
    expect(screen.getByText('Payments')).toBeTruthy();
    expect(screen.getByText('Payment History')).toBeTruthy();
    expect(screen.getByText('PAY-1')).toBeTruthy();
    expect(screen.getByText('PAY-2')).toBeTruthy();
    expect(screen.getByText('Westside Orthopedic')).toBeTruthy();

    fireEvent.press(screen.getByLabelText('Collapse Payments'));
    expect(screen.queryByText('PAY-1')).toBeNull();
    expect(screen.getByLabelText('Expand Payments')).toBeTruthy();

    fireEvent.press(screen.getByLabelText('Collapse Payment History'));
    expect(screen.queryByText('PAY-2')).toBeNull();
    expect(screen.getByLabelText('Expand Payment History')).toBeTruthy();

    fireEvent.press(screen.getByText('History'));
    expect(screen.getByText('Servicing History')).toBeTruthy();
    expect(screen.getByText('Status updated')).toBeTruthy();
    expect(screen.getByText('Case moved to Pre-demand')).toBeTruthy();
  });
});
