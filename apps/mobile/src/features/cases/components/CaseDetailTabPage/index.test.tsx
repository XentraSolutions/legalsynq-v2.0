import { Text } from 'react-native';
import { render } from '@testing-library/react-native';

import { CaseDetailTabPage } from './index';

describe('CaseDetailTabPage', () => {
  it('renders supplied tab content in the scrollable page shell', () => {
    const { getByTestId, getByText } = render(
      <CaseDetailTabPage testID="test-page">
        <Text>Tab content</Text>
      </CaseDetailTabPage>
    );

    expect(getByTestId('test-page')).toBeTruthy();
    expect(getByText('Tab content')).toBeTruthy();
  });
});
