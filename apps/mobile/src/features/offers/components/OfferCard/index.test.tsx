import { render } from '@testing-library/react-native';

import { OFFERS } from '@/features/mockData';

import { OfferCard } from './index';

describe('OfferCard', () => {
  it('renders offer details', async () => {
    const { getByText } = await render(<OfferCard direction="received" offer={OFFERS[0]} />);

    expect(getByText('PENDING')).toBeOnTheScreen();
    expect(getByText('Offer: $118,000')).toBeOnTheScreen();
  });
});
