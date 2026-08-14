import { render } from '@testing-library/react-native';

import { LIENS } from '@/features/mockData';

import { LienCard } from './index';

describe('LienCard', () => {
  it('renders lien summary details', async () => {
    const { getByText } = await render(<LienCard lien={LIENS[0]} />);

    expect(getByText('Patient: John D.')).toBeOnTheScreen();
    expect(getByText('AVAILABLE')).toBeOnTheScreen();
    expect(getByText('$125,000')).toBeOnTheScreen();
  });
});
