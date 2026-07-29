import type { LienView } from './mockData';
import { CASES, DEMO_USER, LIENS, NOTES, OFFERS, delay } from './mockData';
import type { Offer, UpdateOfferRequest } from '@/shared/api/endpoints/Liens';
import type { Note } from '@/shared/api/endpoints/Cases';

let liens = [...LIENS];
let offers = [...OFFERS];
let notes = [...NOTES];

export const MockStore = {
  async getDashboard() {
    return delay({
      myLiens: liens.filter((lien) => lien.sellerId === DEMO_USER.id).length,
      pendingOffers: offers.filter((offer) => offer.status === 'PENDING').length,
      availableMarket: liens.filter((lien) => lien.status === 'AVAILABLE').length,
      openCases: CASES.filter((caseItem) => caseItem.status === 'OPEN').length,
      activities: [
        {
          id: 'act-001',
          title: 'Offer received on John D.',
          subtitle: 'Capital Lien Buyers offered $118,000',
          orgName: 'Capital Lien Buyers',
          time: '2h',
        },
        {
          id: 'act-002',
          title: 'Lien listed',
          subtitle: 'Ethan R. is now available in the marketplace',
          orgName: 'Northstar Legal',
          time: '1d',
        },
        {
          id: 'act-003',
          title: 'Offer accepted',
          subtitle: 'Luis T. moved to sold',
          orgName: 'Zenith Lien Capital',
          time: '3d',
        },
        {
          id: 'act-004',
          title: 'Document uploaded',
          subtitle: 'medical_records.pdf added to John D.',
          orgName: 'Smith Law Firm',
          time: '3d',
        },
        {
          id: 'act-005',
          title: 'Case updated',
          subtitle: 'Maria S. moved to active review',
          orgName: 'Rivera Injury Group',
          time: '4d',
        },
      ],
    });
  },

  async listLiens() {
    return delay([...liens]);
  },

  async getLien(id: string) {
    return delay(liens.find((lien) => lien.id === id) ?? liens[0]);
  },

  async sellLien(lien: Pick<LienView, 'patientName' | 'caseType' | 'jurisdiction' | 'lienAmount' | 'askingPrice'>) {
    const created: LienView = {
      id: `lien-${Date.now()}`,
      caseReference: `LS-${Date.now().toString().slice(-5)}`,
      patientName: lien.patientName,
      caseType: lien.caseType,
      lienAmount: lien.lienAmount,
      askingPrice: lien.askingPrice,
      status: 'AVAILABLE',
      jurisdiction: lien.jurisdiction,
      incidentDate: new Date().toISOString(),
      listedAt: new Date().toISOString(),
      sellerId: DEMO_USER.id,
      organizationId: DEMO_USER.organization.id,
      tenantId: DEMO_USER.tenantId,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      sellerOrgName: DEMO_USER.organization.name,
      offerCount: 0,
      documents: [],
    };

    liens = [created, ...liens];
    return delay(created);
  },

  async listOffers() {
    return delay([...offers]);
  },

  async getOffer(id: string) {
    return delay(offers.find((offer) => offer.id === id) ?? offers[0]);
  },

  async makeOffer(lienId: string, offerAmount: number, notesText?: string) {
    const created: Offer = {
      id: `offer-${Date.now()}`,
      lienId,
      buyerId: DEMO_USER.id,
      buyerOrgName: DEMO_USER.organization.name,
      offerAmount,
      status: 'PENDING',
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
      notes: notesText,
      createdAt: new Date().toISOString(),
    };

    offers = [created, ...offers];
    liens = liens.map((lien) =>
      lien.id === lienId ? { ...lien, offerCount: lien.offerCount + 1, status: 'PENDING' } : lien
    );
    return delay(created);
  },

  async updateOffer(id: string, update: UpdateOfferRequest) {
    offers = offers.map((offer) => (offer.id === id ? { ...offer, ...update } : offer));
    return delay(offers.find((offer) => offer.id === id) ?? offers[0]);
  },

  async listCases() {
    return delay([...CASES]);
  },

  async getCase(id: string) {
    return delay(CASES.find((caseItem) => caseItem.id === id) ?? CASES[0]);
  },

  async getCaseNotes(caseId: string) {
    return delay(notes.filter((note) => note.caseId === caseId));
  },

  async addCaseNote(caseId: string, content: string) {
    const created: Note = {
      id: `note-${Date.now()}`,
      caseId,
      authorId: DEMO_USER.id,
      authorName: `${DEMO_USER.firstName} ${DEMO_USER.lastName}`,
      content,
      createdAt: new Date().toISOString(),
    };

    notes = [created, ...notes];
    return delay(created);
  },
};
