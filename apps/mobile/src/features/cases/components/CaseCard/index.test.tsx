import { fireEvent, render } from '@testing-library/react-native';

import { CaseCard } from './index';

describe('CaseCard', () => {
  it('renders the complete case summary and view action', () => {
    const onPress = jest.fn();
    const { getByText } = render(
      <CaseCard
        caseItem={{
          accidentType: 'Motor Vehicle',
          accidentTypeId: 'motor-vehicle',
          caseManager: 'Morgan Lee',
          caseManagerId: 'manager-1',
          caseNumber: '24–18743',
          clientName: 'Marcus Delgado',
          dateOfLoss: '2025-03-14',
          id: 'case-1',
          lawFirm: 'Harrison & Cole LLP',
          lawFirmId: 'firm-1',
          status: 'Pre-demand',
          updatedAt: '2025-03-15',
        }}
        onPress={onPress}
      />
    );

    expect(getByText('Accident Type')).toBeTruthy();
    expect(getByText('pulse')).toBeTruthy();
    expect(getByText('Motor Vehicle')).toBeTruthy();
    expect(getByText('Law Firm')).toBeTruthy();
    expect(getByText('scale-unbalanced')).toBeTruthy();
    expect(getByText('Harrison & Cole LLP')).toBeTruthy();
    expect(getByText('Date of Loss')).toBeTruthy();
    expect(getByText('03/14/2025')).toBeTruthy();

    fireEvent.press(getByText('View Case'));
    expect(onPress).toHaveBeenCalledTimes(1);
  });
});
