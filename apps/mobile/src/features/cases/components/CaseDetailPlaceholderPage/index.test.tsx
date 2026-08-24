import { render } from '@testing-library/react-native';

import { CaseDetailPlaceholderPage } from './index';

describe('CaseDetailPlaceholderPage', () => {
  it('provides a reusable tab content template', () => {
    const { getByTestId, getByText } = render(<CaseDetailPlaceholderPage title="Documents" />);

    expect(getByTestId('case-documents-page')).toBeTruthy();
    expect(getByText('Documents')).toBeTruthy();
    expect(getByText('This section is ready for case-specific content.')).toBeTruthy();
  });
});
