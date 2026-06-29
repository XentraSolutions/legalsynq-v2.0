'use client';

import { useState } from 'react';
import { useLienStore } from '@/stores/lien-store';
import { FormModal, ConfirmDialog } from '@/components/lien/modal';
import { ActionMenu } from '@/components/lien/action-menu';
import Field from '@/components/lien/field';

// Temp constants — replace with API enum once available
const LAW_FIRM_ROLES = ['CASE_MANAGER', 'ATTORNEY', 'OTHERS'] as const;
type LawFirmRole = (typeof LAW_FIRM_ROLES)[number];

const ROLE_LABELS: Record<LawFirmRole, string> = {
  CASE_MANAGER: 'Case Manager',
  ATTORNEY: 'Attorney',
  OTHERS: 'Others',
};

interface LawFirmContact {
  id: string;
  firstName: string;
  lastName: string;
  role: LawFirmRole;
  email: string;
  phone: string;
}

interface Props {
  lawFirmId: string;
}

const INITIAL_FORM = { firstName: '', lastName: '', role: 'CASE_MANAGER' as LawFirmRole, email: '', phone: '' };
const PAGE_SIZE = 12;

let _nextId = 1;

export function LawFirmContactSection({ lawFirmId: _lawFirmId }: Props) {
  const addToast = useLienStore((s) => s.addToast);
  const [contacts, setContacts] = useState<LawFirmContact[]>([]);
  const [viewMode, setViewMode] = useState<'tile' | 'list'>('tile');
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<LawFirmContact | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<LawFirmContact | null>(null);
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const openAdd = () => {
    setEditTarget(null);
    setForm({ ...INITIAL_FORM });
    setErrors({});
    setModalOpen(true);
  };

  const openEdit = (c: LawFirmContact) => {
    setEditTarget(c);
    setForm({ firstName: c.firstName, lastName: c.lastName, role: c.role, email: c.email, phone: c.phone });
    setErrors({});
    setModalOpen(true);
  };

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.firstName.trim()) e.firstName = 'First Name is required';
    if (!form.lastName.trim()) e.lastName = 'Last Name is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      if (editTarget) {
        setContacts((prev) =>
          prev.map((c) =>
            c.id === editTarget.id
              ? { ...c, firstName: form.firstName.trim(), lastName: form.lastName.trim(), role: form.role, email: form.email.trim(), phone: form.phone.trim() }
              : c
          )
        );
        addToast({ type: 'success', title: 'Contact Updated', description: 'Law firm contact has been updated.' });
      } else {
        const newContact: LawFirmContact = {
          id: String(_nextId++),
          firstName: form.firstName.trim(),
          lastName: form.lastName.trim(),
          role: form.role,
          email: form.email.trim(),
          phone: form.phone.trim(),
        };
        setContacts((prev) => [...prev, newContact]);
        addToast({ type: 'success', title: 'Contact Added', description: 'Law firm contact has been added.' });
      }
      setModalOpen(false);
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = () => {
    if (!deleteTarget) return;
    setContacts((prev) => prev.filter((c) => c.id !== deleteTarget.id));
    addToast({ type: 'success', title: 'Contact Removed', description: `${deleteTarget.firstName} ${deleteTarget.lastName} has been removed.` });
    setDeleteTarget(null);
  };

  const totalPages = Math.ceil(contacts.length / PAGE_SIZE);
  const paged = contacts.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <i className="ri-scales-3-line text-gray-500" />
          <h3 className="text-sm font-semibold text-gray-800">Law Firm Contacts</h3>
          <span className="text-xs text-gray-400 bg-gray-100 rounded-full px-2 py-0.5">{contacts.length}</span>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setViewMode('tile')}
            title="Tile view"
            className={`p-1.5 rounded-lg transition-colors ${viewMode === 'tile' ? 'bg-primary/10 text-primary' : 'text-gray-400 hover:bg-gray-100'}`}
          >
            <i className="ri-layout-grid-line text-base" />
          </button>
          <button
            onClick={() => setViewMode('list')}
            title="List view"
            className={`p-1.5 rounded-lg transition-colors ${viewMode === 'list' ? 'bg-primary/10 text-primary' : 'text-gray-400 hover:bg-gray-100'}`}
          >
            <i className="ri-list-unordered text-base" />
          </button>
          <button
            onClick={openAdd}
            className="flex items-center gap-1.5 text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90"
          >
            <i className="ri-add-line" />
            Add Contact
          </button>
        </div>
      </div>

      <div className="p-5">
        {contacts.length === 0 ? (
          <div className="text-center py-10 text-sm text-gray-400">No contacts yet. Add the first one.</div>
        ) : viewMode === 'tile' ? (
          <TileView contacts={paged} onEdit={openEdit} onDelete={setDeleteTarget} />
        ) : (
          <ListView contacts={paged} onEdit={openEdit} onDelete={setDeleteTarget} />
        )}

        {totalPages > 1 && (
          <div className="flex items-center justify-center gap-2 mt-4 pt-4 border-t border-gray-100">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40"
            >
              Previous
            </button>
            <span className="text-sm text-gray-500">Page {page} of {totalPages}</span>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
              className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40"
            >
              Next
            </button>
          </div>
        )}
      </div>

      <FormModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        onSubmit={handleSubmit}
        title={editTarget ? 'Edit Law Firm Contact' : 'Add New Law Firm Contact'}
        subtitle="Law Firm Contact"
        submitLabel={submitting ? (editTarget ? 'Saving...' : 'Creating...') : 'Save'}
        submitDisabled={submitting || !form.firstName || !form.lastName}
      >
        <div className="space-y-4">
          <Field
            label="First Name"
            required
            value={form.firstName}
            onChange={(v) => setForm({ ...form, firstName: v.toString() })}
            error={errors.firstName}
            placeholder=""
          />
          <Field
            label="Last Name"
            required
            value={form.lastName}
            onChange={(v) => setForm({ ...form, lastName: v.toString() })}
            error={errors.lastName}
            placeholder=""
          />
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-gray-700">Role</label>
            <select
              value={form.role}
              onChange={(e) => setForm({ ...form, role: e.target.value as LawFirmRole })}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-primary/30"
            >
              {LAW_FIRM_ROLES.map((r) => (
                <option key={r} value={r}>{ROLE_LABELS[r]}</option>
              ))}
            </select>
          </div>
          <Field
            label="Telephone Number"
            type="tel"
            value={form.phone}
            onChange={(v) => setForm({ ...form, phone: v.toString() })}
            placeholder=""
          />
          <Field
            label="Email Address"
            type="email"
            value={form.email}
            onChange={(v) => setForm({ ...form, email: v.toString() })}
            placeholder=""
          />
        </div>
      </FormModal>

      {deleteTarget && (
        <ConfirmDialog
          open
          onClose={() => setDeleteTarget(null)}
          onConfirm={handleDelete}
          title="Delete Contact"
          description={`Are you sure you want to delete ${deleteTarget.firstName} ${deleteTarget.lastName}?`}
          confirmLabel="Delete"
          confirmVariant="danger"
        />
      )}
    </div>
  );
}

function TileView({
  contacts,
  onEdit,
  onDelete,
}: {
  contacts: LawFirmContact[];
  onEdit: (c: LawFirmContact) => void;
  onDelete: (c: LawFirmContact) => void;
}) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      {contacts.map((c) => (
        <div key={c.id} className="border border-gray-200 rounded-xl p-4 hover:shadow-sm transition-shadow">
          <div className="flex items-start justify-between mb-3">
            <div>
              <p className="text-sm font-semibold text-gray-900 leading-snug">
                {c.firstName} {c.lastName}
              </p>
              <span className="inline-block mt-0.5 text-xs text-primary bg-primary/10 rounded-full px-2 py-0.5">
                {ROLE_LABELS[c.role]}
              </span>
            </div>
            <ActionMenu
              items={[
                { label: 'Edit Contact', icon: 'ri-edit-line', onClick: () => onEdit(c) },
                { label: 'Delete', icon: 'ri-delete-bin-line', onClick: () => onDelete(c), variant: 'danger', divider: true },
              ]}
            />
          </div>
          <div className="flex items-center gap-2 text-xs text-gray-500 mb-1.5">
            <i className="ri-mail-line text-gray-400 shrink-0" />
            <span className="truncate">{c.email || '--'}</span>
          </div>
          <div className="flex items-center gap-2 text-xs text-gray-500">
            <i className="ri-phone-line text-gray-400 shrink-0" />
            <span>{c.phone || '--'}</span>
          </div>
        </div>
      ))}
    </div>
  );
}

function ListView({
  contacts,
  onEdit,
  onDelete,
}: {
  contacts: LawFirmContact[];
  onEdit: (c: LawFirmContact) => void;
  onDelete: (c: LawFirmContact) => void;
}) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-gray-100">
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">First Name</th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">Last Name</th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">Role</th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">Email</th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">Phone</th>
            <th className="px-3 py-2.5" />
          </tr>
        </thead>
        <tbody>
          {contacts.map((c) => (
            <tr key={c.id} className="border-b border-gray-50 hover:bg-gray-50/50">
              <td className="px-3 py-3 text-gray-900 font-medium">{c.firstName}</td>
              <td className="px-3 py-3 text-gray-700">{c.lastName}</td>
              <td className="px-3 py-3">
                <span className="text-xs text-primary bg-primary/10 rounded-full px-2 py-0.5">{ROLE_LABELS[c.role]}</span>
              </td>
              <td className="px-3 py-3 text-gray-500">{c.email || '—'}</td>
              <td className="px-3 py-3 text-gray-500">{c.phone || '—'}</td>
              <td className="px-3 py-3">
                <ActionMenu
                  items={[
                    { label: 'Edit Contact', icon: 'ri-edit-line', onClick: () => onEdit(c) },
                    { label: 'Delete', icon: 'ri-delete-bin-line', onClick: () => onDelete(c), variant: 'danger', divider: true },
                  ]}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
