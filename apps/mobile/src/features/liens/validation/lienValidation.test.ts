import { sellLienSchema } from './lienValidation';

describe('sellLienSchema', () => {
  it('validates the sell lien form payload', () => {
    const result = sellLienSchema.safeParse({
      patientFirstName: 'John',
      patientLastName: 'Doe',
      caseType: 'AUTO_ACCIDENT',
      incidentDate: '03/15/2023',
      jurisdiction: 'Miami, FL',
      caseReference: 'PI-2026-1042',
      lienAmount: '180000',
      askingPrice: '125000',
      notes: '',
    });

    expect(result.success).toBe(true);
  });
});
