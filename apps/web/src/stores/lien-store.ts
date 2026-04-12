import { create } from 'zustand';
import type {
  CaseSummary, CaseDetail, LienSummary, LienDetail,
  BillOfSaleSummary, BillOfSaleDetail, ServicingItem, ServicingDetail,
  ContactSummary, ContactDetail, DocumentSummary, DocumentDetail,
  LienUser, LienUserDetail, LienOfferSummary,
} from '@/types/lien';
import {
  MOCK_CASES, MOCK_CASE_DETAILS, MOCK_LIENS, MOCK_LIEN_DETAILS, MOCK_LIEN_HISTORY,
  MOCK_BILLS_OF_SALE, MOCK_BOS_DETAILS, MOCK_SERVICING, MOCK_SERVICING_DETAILS,
  MOCK_CONTACTS, MOCK_CONTACT_DETAILS, MOCK_DOCUMENTS, MOCK_DOCUMENT_DETAILS,
  MOCK_USERS, MOCK_USER_DETAILS, MOCK_RECENT_ACTIVITY,
} from '@/lib/lien-mock-data';

export type AppRole = 'Admin' | 'Case Manager' | 'Analyst' | 'Viewer';

export interface ActivityEntry {
  id: string;
  type: string;
  description: string;
  actor: string;
  timestamp: string;
  icon: string;
  color: string;
}

export interface ToastMessage {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  description?: string;
}

interface LienStore {
  currentRole: AppRole;
  setCurrentRole: (role: AppRole) => void;

  toasts: ToastMessage[];
  addToast: (toast: Omit<ToastMessage, 'id'>) => void;
  removeToast: (id: string) => void;

  cases: CaseSummary[];
  caseDetails: Record<string, CaseDetail>;
  addCase: (c: CaseSummary) => void;
  updateCase: (id: string, updates: Partial<CaseSummary>) => void;
  getCaseDetail: (id: string) => CaseDetail | CaseSummary | undefined;

  liens: LienSummary[];
  lienDetails: Record<string, LienDetail>;
  addLien: (l: LienSummary) => void;
  updateLien: (id: string, updates: Partial<LienSummary>) => void;
  addOffer: (lienId: string, offer: LienOfferSummary) => void;
  updateOffer: (lienId: string, offerId: string, updates: Partial<LienOfferSummary>) => void;
  getLienDetail: (id: string) => LienDetail | LienSummary | undefined;

  billsOfSale: BillOfSaleSummary[];
  bosDetails: Record<string, BillOfSaleDetail>;
  addBos: (b: BillOfSaleSummary) => void;
  updateBos: (id: string, updates: Partial<BillOfSaleSummary>) => void;

  servicing: ServicingItem[];
  servicingDetails: Record<string, ServicingDetail>;
  addServicingTask: (s: ServicingItem) => void;
  updateServicing: (id: string, updates: Partial<ServicingItem>) => void;

  contacts: ContactSummary[];
  contactDetails: Record<string, ContactDetail>;
  addContact: (c: ContactSummary) => void;
  updateContact: (id: string, updates: Partial<ContactSummary>) => void;

  documents: DocumentSummary[];
  documentDetails: Record<string, DocumentDetail>;
  addDocument: (d: DocumentSummary) => void;
  updateDocument: (id: string, updates: Partial<DocumentSummary>) => void;

  users: LienUser[];
  userDetails: Record<string, LienUserDetail>;
  addUser: (u: LienUser) => void;
  updateUser: (id: string, updates: Partial<LienUser>) => void;

  activity: ActivityEntry[];
  addActivity: (entry: Omit<ActivityEntry, 'id'>) => void;

  caseNotes: Record<string, { id: string; text: string; author: string; timestamp: string }[]>;
  addCaseNote: (caseId: string, text: string) => void;
}

let toastCounter = 0;
let activityCounter = 100;
let noteCounter = 0;

function genId(prefix: string) {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
}

export const useLienStore = create<LienStore>((set, get) => ({
  currentRole: 'Admin',
  setCurrentRole: (role) => set({ currentRole: role }),

  toasts: [],
  addToast: (toast) => {
    const id = `toast-${++toastCounter}`;
    set((s) => ({ toasts: [...s.toasts, { ...toast, id }] }));
    setTimeout(() => get().removeToast(id), 4000);
  },
  removeToast: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),

  cases: [...MOCK_CASES],
  caseDetails: { ...MOCK_CASE_DETAILS },
  addCase: (c) => {
    set((s) => ({ cases: [c, ...s.cases] }));
    get().addActivity({ type: 'case_create', description: `Case ${c.caseNumber} created for ${c.clientName}`, actor: 'Current User', timestamp: new Date().toISOString(), icon: 'ri-folder-add-line', color: 'text-blue-600' });
    get().addToast({ type: 'success', title: 'Case Created', description: `${c.caseNumber} has been created` });
  },
  updateCase: (id, updates) => {
    set((s) => ({
      cases: s.cases.map((c) => c.id === id ? { ...c, ...updates, updatedAtUtc: new Date().toISOString() } : c),
      caseDetails: s.caseDetails[id] ? { ...s.caseDetails, [id]: { ...s.caseDetails[id], ...updates, updatedAtUtc: new Date().toISOString() } } : s.caseDetails,
    }));
  },
  getCaseDetail: (id) => get().caseDetails[id] ?? get().cases.find((c) => c.id === id),

  liens: [...MOCK_LIENS],
  lienDetails: { ...MOCK_LIEN_DETAILS },
  addLien: (l) => {
    set((s) => ({ liens: [l, ...s.liens] }));
    get().addActivity({ type: 'lien_create', description: `Lien ${l.lienNumber} created`, actor: 'Current User', timestamp: new Date().toISOString(), icon: 'ri-stack-line', color: 'text-indigo-600' });
    get().addToast({ type: 'success', title: 'Lien Created', description: `${l.lienNumber} has been created` });
  },
  updateLien: (id, updates) => {
    set((s) => ({
      liens: s.liens.map((l) => l.id === id ? { ...l, ...updates, updatedAtUtc: new Date().toISOString() } : l),
      lienDetails: s.lienDetails[id] ? { ...s.lienDetails, [id]: { ...s.lienDetails[id], ...updates, updatedAtUtc: new Date().toISOString() } } : s.lienDetails,
    }));
  },
  addOffer: (lienId, offer) => {
    set((s) => {
      const existing = s.lienDetails[lienId];
      if (existing) {
        return { lienDetails: { ...s.lienDetails, [lienId]: { ...existing, offers: [...(existing.offers || []), offer] } } };
      }
      const summary = s.liens.find((l) => l.id === lienId);
      if (summary) {
        return { lienDetails: { ...s.lienDetails, [lienId]: { ...summary, offers: [offer] } as LienDetail } };
      }
      return {};
    });
    get().addActivity({ type: 'lien_offer', description: `Offer of $${offer.offerAmount.toLocaleString()} submitted on ${lienId}`, actor: offer.buyerOrgName || 'Buyer', timestamp: new Date().toISOString(), icon: 'ri-money-dollar-circle-line', color: 'text-green-600' });
  },
  updateOffer: (lienId, offerId, updates) => {
    set((s) => {
      const detail = s.lienDetails[lienId];
      if (!detail?.offers) return {};
      return {
        lienDetails: {
          ...s.lienDetails,
          [lienId]: {
            ...detail,
            offers: detail.offers.map((o) => o.id === offerId ? { ...o, ...updates, updatedAtUtc: new Date().toISOString() } : o),
          },
        },
      };
    });
  },
  getLienDetail: (id) => get().lienDetails[id] ?? get().liens.find((l) => l.id === id),

  billsOfSale: [...MOCK_BILLS_OF_SALE],
  bosDetails: { ...MOCK_BOS_DETAILS },
  addBos: (b) => {
    set((s) => ({ billsOfSale: [b, ...s.billsOfSale] }));
    get().addToast({ type: 'success', title: 'Bill of Sale Created', description: `${b.bosNumber} has been created` });
  },
  updateBos: (id, updates) => {
    set((s) => ({
      billsOfSale: s.billsOfSale.map((b) => b.id === id ? { ...b, ...updates } : b),
      bosDetails: s.bosDetails[id] ? { ...s.bosDetails, [id]: { ...s.bosDetails[id], ...updates } } : s.bosDetails,
    }));
  },

  servicing: [...MOCK_SERVICING],
  servicingDetails: { ...MOCK_SERVICING_DETAILS },
  addServicingTask: (s) => {
    set((st) => ({ servicing: [s, ...st.servicing] }));
    get().addToast({ type: 'success', title: 'Task Created', description: `${s.taskNumber} assigned to ${s.assignedTo}` });
  },
  updateServicing: (id, updates) => {
    set((s) => ({
      servicing: s.servicing.map((sv) => sv.id === id ? { ...sv, ...updates, updatedAtUtc: new Date().toISOString() } : sv),
      servicingDetails: s.servicingDetails[id]
        ? { ...s.servicingDetails, [id]: { ...s.servicingDetails[id], ...updates, updatedAtUtc: new Date().toISOString() } }
        : s.servicingDetails,
    }));
  },

  contacts: [...MOCK_CONTACTS],
  contactDetails: { ...MOCK_CONTACT_DETAILS },
  addContact: (c) => {
    set((s) => ({ contacts: [c, ...s.contacts] }));
    get().addToast({ type: 'success', title: 'Contact Added', description: `${c.name} has been added` });
  },
  updateContact: (id, updates) => {
    set((s) => ({
      contacts: s.contacts.map((c) => c.id === id ? { ...c, ...updates } : c),
      contactDetails: s.contactDetails[id] ? { ...s.contactDetails, [id]: { ...s.contactDetails[id], ...updates } } : s.contactDetails,
    }));
  },

  documents: [...MOCK_DOCUMENTS],
  documentDetails: { ...MOCK_DOCUMENT_DETAILS },
  addDocument: (d) => {
    set((s) => ({ documents: [d, ...s.documents] }));
    get().addToast({ type: 'success', title: 'Document Uploaded', description: `${d.fileName} uploaded successfully` });
  },
  updateDocument: (id, updates) => {
    set((s) => ({
      documents: s.documents.map((d) => d.id === id ? { ...d, ...updates } : d),
      documentDetails: s.documentDetails[id] ? { ...s.documentDetails, [id]: { ...s.documentDetails[id], ...updates } } : s.documentDetails,
    }));
  },

  users: [...MOCK_USERS],
  userDetails: { ...MOCK_USER_DETAILS },
  addUser: (u) => {
    set((s) => ({ users: [u, ...s.users] }));
    get().addToast({ type: 'success', title: 'User Invited', description: `Invitation sent to ${u.email}` });
  },
  updateUser: (id, updates) => {
    set((s) => ({
      users: s.users.map((u) => u.id === id ? { ...u, ...updates } : u),
      userDetails: s.userDetails[id] ? { ...s.userDetails, [id]: { ...s.userDetails[id], ...updates } } : s.userDetails,
    }));
  },

  activity: MOCK_RECENT_ACTIVITY.map((a) => ({ ...a })),
  addActivity: (entry) => {
    const id = `act-${++activityCounter}`;
    set((s) => ({ activity: [{ ...entry, id }, ...s.activity].slice(0, 50) }));
  },

  caseNotes: {},
  addCaseNote: (caseId, text) => {
    const note = { id: genId('note'), text, author: 'Current User', timestamp: new Date().toISOString() };
    set((s) => ({
      caseNotes: { ...s.caseNotes, [caseId]: [...(s.caseNotes[caseId] || []), note] },
    }));
    get().addToast({ type: 'success', title: 'Note Added' });
  },
}));

export function canPerformAction(role: AppRole, action: 'create' | 'edit' | 'delete' | 'approve' | 'assign' | 'view'): boolean {
  switch (role) {
    case 'Admin': return true;
    case 'Case Manager': return action !== 'delete';
    case 'Analyst': return action === 'view' || action === 'edit';
    case 'Viewer': return action === 'view';
    default: return false;
  }
}
