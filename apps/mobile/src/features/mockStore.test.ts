import { MockStore } from './mockStore';

describe('MockStore', () => {
  it('creates offers and reflects them in the offer list', async () => {
    const offer = await MockStore.makeOffer('lien-001', 117500, 'Fast close');
    const offers = await MockStore.listOffers();

    expect(offer.offerAmount).toBe(117500);
    expect(offers.some((item) => item.id === offer.id)).toBe(true);
  });

  it('adds case notes', async () => {
    const note = await MockStore.addCaseNote('case-001', 'Follow up with buyer.');
    const notes = await MockStore.getCaseNotes('case-001');

    expect(note.content).toBe('Follow up with buyer.');
    expect(notes.some((item) => item.id === note.id)).toBe(true);
  });
});
