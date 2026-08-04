import type { Lien, LienCaseType, LienStatus, Offer } from '@/shared/api/endpoints/Liens';
import type { Case, Note } from '@/shared/api/endpoints/Cases';
import type { UserSession } from '@/shared/types/auth';

export interface DocumentAttachment {
  id: string;
  filename: string;
}

export interface LienView extends Lien {
  sellerOrgName: string;
  offerCount: number;
  buyerOrgName?: string;
  documents: DocumentAttachment[];
}

export interface CaseView extends Case {
  assignedAttorney: string;
  linkedLienIds: string[];
}

export interface ActivityItem {
  id: string;
  title: string;
  subtitle: string;
  orgName: string;
  time: string;
}

export interface DashboardSummary {
  myLiens: number;
  pendingOffers: number;
  availableMarket: number;
  openCases: number;
  activities: ActivityItem[];
}

export const DEMO_USER: UserSession = {
  id: 'usr-demo-1',
  email: 'avery.mendoza@smithlaw.example',
  firstName: 'Avery',
  lastName: 'Mendoza',
  roles: ['TenantAdmin', 'SYNQLIEN_SELLER', 'SYNQLIEN_BUYER'],
  permissions: ['liens.read', 'liens.write', 'offers.write'],
  organization: {
    id: 'org-smith-law',
    name: 'Smith Law Firm',
    tenantId: 'tenant-demo',
  },
  tenantId: 'tenant-demo',
};

export const LIENS: LienView[] = [
  {
    id: 'lien-001',
    caseReference: 'PI-2026-1042',
    patientName: 'John D.',
    caseType: 'AUTO_ACCIDENT',
    lienAmount: 180000,
    askingPrice: 125000,
    status: 'AVAILABLE',
    jurisdiction: 'Miami, FL',
    incidentDate: '2023-03-15',
    listedAt: '2026-06-21T09:00:00Z',
    sellerId: 'usr-seller-1',
    organizationId: 'org-smith-law',
    tenantId: 'tenant-demo',
    createdAt: '2026-06-20T16:00:00Z',
    updatedAt: '2026-06-21T09:00:00Z',
    sellerOrgName: 'Smith Law Firm',
    offerCount: 2,
    documents: [
      { id: 'doc-001', filename: 'medical_records.pdf' },
      { id: 'doc-002', filename: 'accident_report.pdf' },
    ],
  },
  {
    id: 'lien-002',
    caseReference: 'WC-2026-2031',
    patientName: 'Maria S.',
    caseType: 'WORKERS_COMP',
    lienAmount: 74000,
    askingPrice: 52000,
    status: 'PENDING',
    jurisdiction: 'Austin, TX',
    incidentDate: '2024-01-12',
    listedAt: '2026-06-18T12:00:00Z',
    sellerId: 'usr-seller-2',
    organizationId: 'org-rivera-law',
    tenantId: 'tenant-demo',
    createdAt: '2026-06-18T12:00:00Z',
    updatedAt: '2026-06-22T08:00:00Z',
    sellerOrgName: 'Rivera Injury Group',
    offerCount: 1,
    documents: [{ id: 'doc-003', filename: 'treatment_summary.pdf' }],
  },
  {
    id: 'lien-003',
    caseReference: 'PI-2026-1477',
    patientName: 'Ethan R.',
    caseType: 'PERSONAL_INJURY',
    lienAmount: 320000,
    askingPrice: 205000,
    status: 'AVAILABLE',
    jurisdiction: 'Phoenix, AZ',
    incidentDate: '2022-11-04',
    listedAt: '2026-06-16T15:00:00Z',
    sellerId: 'usr-seller-3',
    organizationId: 'org-northstar',
    tenantId: 'tenant-demo',
    createdAt: '2026-06-15T18:00:00Z',
    updatedAt: '2026-06-16T15:00:00Z',
    sellerOrgName: 'Northstar Legal',
    offerCount: 4,
    documents: [{ id: 'doc-004', filename: 'case_evaluation.pdf' }],
  },
  {
    id: 'lien-004',
    caseReference: 'MM-2026-8821',
    patientName: 'Priya K.',
    caseType: 'MEDICAL_MALPRACTICE',
    lienAmount: 48000,
    askingPrice: 35000,
    status: 'DRAFT',
    jurisdiction: 'Chicago, IL',
    incidentDate: '2024-08-22',
    sellerId: DEMO_USER.id,
    organizationId: DEMO_USER.organization.id,
    tenantId: 'tenant-demo',
    createdAt: '2026-06-19T14:00:00Z',
    updatedAt: '2026-06-19T14:00:00Z',
    sellerOrgName: DEMO_USER.organization.name,
    offerCount: 0,
    documents: [],
  },
  {
    id: 'lien-005',
    caseReference: 'PI-2026-3108',
    patientName: 'Luis T.',
    caseType: 'AUTO_ACCIDENT',
    lienAmount: 265000,
    askingPrice: 158000,
    status: 'SOLD',
    jurisdiction: 'Orlando, FL',
    incidentDate: '2023-07-09',
    listedAt: '2026-06-08T10:00:00Z',
    sellerId: DEMO_USER.id,
    buyerId: 'buyer-zenith',
    organizationId: DEMO_USER.organization.id,
    tenantId: 'tenant-demo',
    createdAt: '2026-06-07T10:00:00Z',
    updatedAt: '2026-06-20T10:00:00Z',
    sellerOrgName: DEMO_USER.organization.name,
    buyerOrgName: 'Zenith Lien Capital',
    offerCount: 3,
    documents: [{ id: 'doc-005', filename: 'settlement_packet.pdf' }],
  },
];

export const OFFERS: Offer[] = [
  {
    id: 'offer-001',
    lienId: 'lien-001',
    buyerId: 'buyer-capital',
    buyerOrgName: 'Capital Lien Buyers',
    offerAmount: 118000,
    status: 'PENDING',
    expiresAt: '2026-06-26T17:00:00Z',
    notes: 'Ready to close within five business days after document review.',
    createdAt: '2026-06-24T09:15:00Z',
  },
  {
    id: 'offer-002',
    lienId: 'lien-003',
    buyerId: DEMO_USER.id,
    buyerOrgName: DEMO_USER.organization.name,
    offerAmount: 190000,
    status: 'PENDING',
    expiresAt: '2026-06-27T17:00:00Z',
    notes: 'Subject to final medical record review.',
    createdAt: '2026-06-23T14:30:00Z',
  },
  {
    id: 'offer-003',
    lienId: 'lien-005',
    buyerId: 'buyer-zenith',
    buyerOrgName: 'Zenith Lien Capital',
    offerAmount: 158000,
    status: 'ACCEPTED',
    expiresAt: '2026-06-22T17:00:00Z',
    notes: 'Accepted at asking price.',
    createdAt: '2026-06-19T11:10:00Z',
  },
];

export const CASES: CaseView[] = [
  {
    id: 'case-001',
    caseReference: 'PI-2026-1042',
    patientName: 'John D.',
    caseType: 'AUTO_ACCIDENT',
    status: 'OPEN',
    jurisdiction: 'Miami, FL',
    incidentDate: '2023-03-15',
    assignedAttorney: 'Nora Smith',
    lienCount: 1,
    linkedLienIds: ['lien-001'],
    organizationId: 'org-smith-law',
    tenantId: 'tenant-demo',
    createdAt: '2026-06-01T09:00:00Z',
    updatedAt: '2026-06-21T09:00:00Z',
  },
  {
    id: 'case-002',
    caseReference: 'WC-2026-2031',
    patientName: 'Maria S.',
    caseType: 'WORKERS_COMP',
    status: 'OPEN',
    jurisdiction: 'Austin, TX',
    incidentDate: '2024-01-12',
    assignedAttorney: 'Elena Rivera',
    lienCount: 2,
    linkedLienIds: ['lien-002'],
    organizationId: 'org-rivera-law',
    tenantId: 'tenant-demo',
    createdAt: '2026-05-29T09:00:00Z',
    updatedAt: '2026-06-18T12:00:00Z',
  },
  {
    id: 'case-003',
    caseReference: 'PI-2026-3108',
    patientName: 'Luis T.',
    caseType: 'AUTO_ACCIDENT',
    status: 'PENDING',
    jurisdiction: 'Orlando, FL',
    incidentDate: '2023-07-09',
    assignedAttorney: 'Avery Mendoza',
    lienCount: 1,
    linkedLienIds: ['lien-005'],
    organizationId: DEMO_USER.organization.id,
    tenantId: 'tenant-demo',
    createdAt: '2026-06-07T10:00:00Z',
    updatedAt: '2026-06-20T10:00:00Z',
  },
];

export const NOTES: Note[] = [
  {
    id: 'note-001',
    caseId: 'case-001',
    authorId: 'usr-seller-1',
    authorName: 'Nora Smith',
    category: 'follow-up',
    content: 'Updated treatment ledger uploaded for buyer review.',
    createdAt: '2026-06-22T15:30:00Z',
    isEdited: false,
    isPinned: false,
  },
  {
    id: 'note-002',
    caseId: 'case-001',
    authorId: DEMO_USER.id,
    authorName: 'Avery Mendoza',
    category: 'general',
    content: 'Confirming jurisdiction and lien amount before counteroffer.',
    createdAt: '2026-06-23T10:15:00Z',
    isEdited: false,
    isPinned: false,
  },
];

export const CASE_TYPE_LABELS: Record<LienCaseType, string> = {
  AUTO_ACCIDENT: 'Auto Accident',
  WORKERS_COMP: 'Workers Comp',
  PERSONAL_INJURY: 'Personal Injury',
  MEDICAL_MALPRACTICE: 'Medical Malpractice',
};

export const STATUS_LABELS: Record<LienStatus, string> = {
  DRAFT: 'Draft',
  AVAILABLE: 'Available',
  PENDING: 'Pending',
  SOLD: 'Sold',
  SETTLED: 'Settled',
  DISPUTED: 'Disputed',
};

export function delay<T>(value: T, milliseconds = 250): Promise<T> {
  return new Promise((resolve) => {
    setTimeout(() => resolve(value), milliseconds);
  });
}
