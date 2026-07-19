'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { CASE_REASSIGN_CONFIG } from '@/lib/contacts';
import type { ContactCaseSummary } from '@/lib/contacts/contacts.types';
import { StatusBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { Pagination } from '@/components/ui/pagination';
import { Modal } from '@/components/lien/modal';
import { ContactPicker } from '@/components/lien/contact-picker';
import { useLienStore } from '@/stores/lien-store';
import { ApiError } from '@/lib/api-client';
import { useContactCases, useReassignContactCase } from '@/hooks/use-contact-cases';

// Contact types with a known case-lookup API. Any other type (e.g.
// LienHolder, CaseManager, InternalUser) has no equivalent endpoint, so the
// section is hidden rather than showing an empty/broken table.
const SUPPORTED_CONTACT_TYPES = [
  'LawFirm',
  'Lead',
  'MedicalFacility',
  'Provider',
  'FundingCompany',
];

// Provider/facility/funding cases are surfaced via their liens, so the table
// also shows lien id + billing/purchase amount. Law firm and lead cases are
// shown at the case level only.
const LIEN_DETAIL_CONTACT_TYPES = ['MedicalFacility', 'Provider', 'FundingCompany'];

const PAGE_SIZE = 10;

const currency = (value: number | null) =>
  value != null ? new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value) : '—';

interface Props {
  contactId: string;
  contactType: string;
}

export function ContactCasesSection({ contactId, contactType }: Props) {
  const router = useRouter();
  const addToast = useLienStore((s) => s.addToast);
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState('');
  const [debouncedKeyword, setDebouncedKeyword] = useState('');
  const supported = SUPPORTED_CONTACT_TYPES.includes(contactType);
  const showLienColumns = LIEN_DETAIL_CONTACT_TYPES.includes(contactType);
  const reassignConfig = CASE_REASSIGN_CONFIG[contactType];

  const [reassignTarget, setReassignTarget] = useState<ContactCaseSummary | null>(null);
  const [newPrimaryId, setNewPrimaryId] = useState('');
  const [newSecondaryId, setNewSecondaryId] = useState('');
  const [reassignModalOpen, setReassignModalOpen] = useState(false);
  const [secondaryContactSubtype, setSecondaryContactSubtype] = useState<string | undefined>();
  const [resolvingSecondarySubtype, setResolvingSecondarySubtype] = useState(false);

const { data, isLoading } = useContactCases(
    contactId,
    contactType,
    { keyword: debouncedKeyword, page, limit: PAGE_SIZE },
    supported,
  );
  const cases = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const loading = supported && isLoading;

  const reassignMutation = useReassignContactCase();
  const submitting = reassignMutation.isPending;

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedKeyword(keyword), 300);
    return () => clearTimeout(timeout);
  }, [keyword]);

  useEffect(() => { setPage(1); }, [debouncedKeyword]);

  // Resolves the (tenant-configurable) secondary contact subtype once the
  // reassign modal opens, so the secondary picker knows which sub-contacts
  // to offer (e.g. case manager role code).
  useEffect(() => {
    if (!reassignTarget || !reassignConfig?.secondary) {
      setSecondaryContactSubtype(undefined);
      setResolvingSecondarySubtype(false);
      return;
    }
    let cancelled = false;
    setResolvingSecondarySubtype(true);
    reassignConfig.secondary.getContactSubtype().then((code) => {
      if (cancelled) return;
      setSecondaryContactSubtype(code);
      setResolvingSecondarySubtype(false);
    });
    return () => { cancelled = true; };
  }, [reassignTarget, reassignConfig]);

  const openReassign = (c: ContactCaseSummary) => {
    setReassignTarget(c);
    setNewPrimaryId('');
    setNewSecondaryId('');
    setReassignModalOpen(true);
  };

  const closeReassign = () => {
    setReassignModalOpen(false);
    setReassignTarget(null);
  };

  const handleReassignSubmit = async () => {
    if (!reassignTarget || !reassignConfig) return;
    // Close the modal immediately so the action feels instant. The
    // selection stays in state (reassignTarget/newPrimaryId/newSecondaryId
    // aren't cleared) so the modal can reopen pre-filled on failure.
    setReassignModalOpen(false);
    try {
      await reassignMutation.mutateAsync({
        contactType,
        item: reassignTarget,
        newPrimaryId,
        newSecondaryId: reassignConfig.secondary ? newSecondaryId : undefined,
      });
      addToast({
        type: 'success',
        title: 'Reassigned',
        description: `${reassignConfig.label} has been reassigned.`,
      });
      setReassignTarget(null);
    } catch (err) {
      addToast({
        type: 'error',
        title: 'Reassign Failed',
        description:
          err instanceof ApiError ? err.message : 'An unexpected error occurred',
      });
      // Reopen with the same selection so the user can retry — never
      // automatically resubmit the request.
      setReassignModalOpen(true);
    }
  };

  const totalPages = Math.max(Math.ceil(totalCount / PAGE_SIZE), 1);

  if (!supported) return null;

  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 gap-3">
        <div className="flex items-center gap-2">
          <i className="ri-folder-2-line text-gray-500" />
          <h3 className="text-sm font-semibold text-gray-800">Cases</h3>
          {!loading && (
            <span className="text-xs text-gray-400 bg-gray-100 rounded-full px-2 py-0.5">{totalCount}</span>
          )}
        </div>
        <div className="relative w-full max-w-xs">
          <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
          <input
            type="text"
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            placeholder="Search cases..."
            className="w-full pl-9 pr-3 py-1.5 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
          />
        </div>
      </div>

      <div className="p-5">
        {loading ? (
          <div className="text-center py-10 text-sm text-gray-400">Loading cases...</div>
        ) : cases.length === 0 ? (
          <div className="text-center py-10 text-sm text-gray-400 italic">No Case Found</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50/80 border-b border-gray-100">
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Case ID</th>
                  {showLienColumns && (
                    <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Lien ID</th>
                  )}
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Full Name</th>
                  {showLienColumns && (
                    <>
                      <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Billing Amount</th>
                      <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Purchase Amount</th>
                    </>
                  )}
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Accident Type</th>
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Date of Loss</th>
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Date of Birth</th>
                  <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="text-right px-3 py-2.5 text-xs font-medium text-gray-500 uppercase tracking-wide">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {cases.map((c) => (
                  <tr key={c.lienId ?? c.id} className="hover:bg-gray-50/50">
                    <td className="px-3 py-3 font-medium text-gray-900">{c.caseNumber}</td>
                    {showLienColumns && (
                      <td className="px-3 py-3 text-gray-600">{c.lienId ?? '—'}</td>
                    )}
                    <td className="px-3 py-3 text-gray-700">{c.personName}</td>
                    {showLienColumns && (
                      <>
                        <td className="px-3 py-3 text-gray-900">{currency(c.billingAmount)}</td>
                        <td className="px-3 py-3 text-gray-900">{currency(c.purchaseAmount)}</td>
                      </>
                    )}
                    <td className="px-3 py-3 text-gray-600">{c.accidentType ?? '—'}</td>
                    <td className="px-3 py-3 text-xs text-gray-500 tabular-nums">{c.dateOfLoss ?? '—'}</td>
                    <td className="px-3 py-3 text-xs text-gray-500 tabular-nums">{c.dateOfBirth ?? '—'}</td>
                    <td className="px-3 py-3">
                      <StatusBadge status={c.status} />
                    </td>
                    <td className="px-3 py-3 text-right">
                      <ActionMenu
                        items={[
                          {
                            label: 'View Case',
                            icon: 'ri-eye-line',
                            onClick: () => router.push(`/lien/cases/${c.id}`),
                          },
                          ...(reassignConfig
                            ? [
                                {
                                  label: 'Reassign',
                                  icon: 'ri-exchange-line',
                                  onClick: () => openReassign(c),
                                },
                              ]
                            : []),
                        ]}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {totalPages > 1 && (
              <div className="flex justify-end pt-4">
                <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
              </div>
            )}
          </div>
        )}
      </div>

      {reassignTarget && reassignConfig && (
        <Modal
          open={reassignModalOpen}
          onClose={closeReassign}
          title="Re-Assign Case"
          size="sm"
          footer={
            <div className="flex items-center justify-between w-full">
              <button
                onClick={closeReassign}
                disabled={submitting}
                className="text-sm font-medium text-primary hover:underline disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                onClick={handleReassignSubmit}
                disabled={submitting || !newPrimaryId}
                className="px-4 py-2 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg disabled:opacity-50 transition-colors"
              >
                {submitting ? 'Assigning...' : 'Assign Case'}
              </button>
            </div>
          }
        >
          <div className="flex items-center gap-2 mb-4">
            <span className="flex items-center justify-center w-7 h-7 rounded-lg bg-primary text-white shrink-0">
              <i className="ri-exchange-line text-sm" />
            </span>
            <span className="text-sm font-semibold text-primary">Case Assignment Details</span>
          </div>
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                <span className="text-red-500 mr-0.5">*</span>
                {reassignConfig.fieldLabel}
              </label>
              <ContactPicker
                contactType={contactType}
                excludeId={contactId}
                value={newPrimaryId}
                onChange={(id) => {
                  setNewPrimaryId(id);
                  setNewSecondaryId('');
                }}
                placeholder="Please select"
                disabled={submitting}
              />
            </div>

            {reassignConfig.secondary && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  {reassignConfig.secondary.label}
                </label>
                <ContactPicker
                  contactType={reassignConfig.secondary.contactType}
                  contactSubtype={secondaryContactSubtype}
                  lawFirmId={reassignConfig.secondary.scopeParam === 'lawFirmId' ? newPrimaryId : undefined}
                  facilityId={reassignConfig.secondary.scopeParam === 'facilityId' ? newPrimaryId : undefined}
                  value={newSecondaryId}
                  onChange={setNewSecondaryId}
                  disabled={submitting || !newPrimaryId || resolvingSecondarySubtype}
                  placeholder="Please select"
                />
              </div>
            )}
          </div>
        </Modal>
      )}
    </div>
  );
}
