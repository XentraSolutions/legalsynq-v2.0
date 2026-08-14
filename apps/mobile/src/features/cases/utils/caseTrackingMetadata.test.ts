import {
  mergeCaseTrackingMetadata,
  parseCaseTrackingMetadata,
} from './caseTrackingMetadata';

describe('case tracking metadata', () => {
  it('reads canonical and legacy aliases', () => {
    const result = parseCaseTrackingMetadata(
      'accidentType=MVA; accidentState=AZ; leadDescription=Jordan Lee; uccFiled=yes'
    );

    expect(result.accidentType).toBe('MVA');
    expect(result.stateOfIncident).toBe('AZ');
    expect(result.lead).toBe('Jordan Lee');
    expect(result.isUccFiled).toBe(true);
  });

  it('updates tracking fields while preserving unrelated metadata', () => {
    const result = mergeCaseTrackingMetadata(
      'Original free-form note; lawFirm=Aaron Law; custom=value',
      {
        currentMedicalStatus: 'Pre-demand',
        shareCase: true,
        stateOfIncident: 'CA',
      }
    );

    expect(result).toContain('Original free-form note');
    expect(result).toContain('lawFirm=Aaron Law');
    expect(result).toContain('custom=value');
    expect(result).toContain('currentMedicalStatus=Pre-demand');
    expect(result).toContain('shareCase=true');
    expect(result).toContain('stateOfIncident=CA');
  });
});
