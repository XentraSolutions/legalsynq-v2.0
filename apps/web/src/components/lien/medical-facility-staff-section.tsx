'use client';

import { useState, useEffect, useCallback } from 'react';
import { useLienStore } from '@/stores/lien-store';
import { facilityService } from '@/lib/facility';
import { type FacilityStaff } from '@/lib/facility/facility.types';
import { ApiError } from '@/lib/api-client';
import { FormModal, ConfirmDialog } from '@/components/lien/modal';
import { ActionMenu } from '@/components/lien/action-menu';
import Field from '@/components/lien/field';

interface Props {
  facilityId: string;
}

const INITIAL_FORM = { firstname: '', lastname: '', email: '', phone: '' };
const PAGE_SIZE = 12;

export function MedicalFacilityStaffSection({ facilityId }: Props) {
  const addToast = useLienStore((s) => s.addToast);
  const [staff, setStaff] = useState<FacilityStaff[]>([]);
  const [loading, setLoading] = useState(true);
  const [viewMode, setViewMode] = useState<'tile' | 'list'>('tile');
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<FacilityStaff | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<FacilityStaff | null>(null);
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const fetchStaff = useCallback(async () => {
    try {
      setLoading(true);
      const result = await facilityService.getContactPersonByFacility(facilityId);
      setStaff(Array.isArray(result) ? result : []);
    } catch {
      setStaff([]);
    } finally {
      setLoading(false);
    }
  }, [facilityId]);

  useEffect(() => { fetchStaff(); }, [fetchStaff]);

  const openAdd = () => {
    setEditTarget(null);
    setForm({ ...INITIAL_FORM });
    setErrors({});
    setModalOpen(true);
  };

  const openEdit = (s: FacilityStaff) => {
    setEditTarget(s);
    setForm({ firstname: s.firstname, lastname: s.lastname, email: s.email, phone: s.phone });
    setErrors({});
    setModalOpen(true);
  };

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.firstname.trim()) e.firstname = 'First Name is required';
    if (!form.lastname.trim()) e.lastname = 'Last Name is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      if (editTarget) {
        await facilityService.updateContactPerson({
          id: editTarget.id,
          firstname: form.firstname.trim(),
          lastname: form.lastname.trim(),
          email: form.email.trim(),
          phone: form.phone.trim(),
          facilityId,
        });
        addToast({ type: 'success', title: 'Staff Updated', description: 'Contact person has been updated.' });
      } else {
        await facilityService.contactPerson({
          firstname: form.firstname.trim(),
          lastname: form.lastname.trim(),
          email: form.email.trim(),
          phone: form.phone.trim(),
          facilityId,
        });
        addToast({ type: 'success', title: 'Staff Added', description: 'Contact person has been added.' });
      }
      setModalOpen(false);
      fetchStaff();
    } catch (err) {
      addToast({
        type: 'error',
        title: editTarget ? 'Update Failed' : 'Create Failed',
        description: err instanceof ApiError ? err.message : 'An unexpected error occurred',
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await facilityService.deleteContactPerson(deleteTarget.id);
      addToast({ type: 'success', title: 'Staff Removed', description: `${deleteTarget.firstname} ${deleteTarget.lastname} has been removed.` });
      setDeleteTarget(null);
      fetchStaff();
    } catch (err) {
      addToast({
        type: 'error',
        title: 'Delete Failed',
        description: err instanceof ApiError ? err.message : 'An unexpected error occurred',
      });
      setDeleteTarget(null);
    }
  };

  const totalPages = Math.ceil(staff.length / PAGE_SIZE);
  const paged = staff.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <i className="ri-team-line text-gray-500" />
          <h3 className="text-sm font-semibold text-gray-800">Medical Facility Staff</h3>
          {!loading && (
            <span className="text-xs text-gray-400 bg-gray-100 rounded-full px-2 py-0.5">{staff.length}</span>
          )}
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
        {loading ? (
          <div className="text-center py-10 text-sm text-gray-400">Loading staff...</div>
        ) : staff.length === 0 ? (
          <div className="text-center py-10 text-sm text-gray-400">No staff members yet. Add the first one.</div>
        ) : viewMode === 'tile' ? (
          <TileView staff={paged} onEdit={openEdit} onDelete={setDeleteTarget} />
        ) : (
          <ListView staff={paged} onEdit={openEdit} onDelete={setDeleteTarget} />
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
        title={editTarget ? 'Edit Medical Facility Staff' : 'Add New Medical Facility Staff'}
        subtitle="Medical Facility Staff"
        submitLabel={submitting ? (editTarget ? 'Saving...' : 'Creating...') : 'Save'}
        submitDisabled={submitting || !form.firstname || !form.lastname}
      >
        <div className="space-y-4">
          <Field
            label="First Name"
            required
            value={form.firstname}
            onChange={(v) => setForm({ ...form, firstname: v.toString() })}
            error={errors.firstname}
            placeholder=""
          />
          <Field
            label="Last Name"
            required
            value={form.lastname}
            onChange={(v) => setForm({ ...form, lastname: v.toString() })}
            error={errors.lastname}
            placeholder=""
          />
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
          title="Delete Staff Member"
          description={`Are you sure you want to delete ${deleteTarget.firstname} ${deleteTarget.lastname}?`}
          confirmLabel="Delete"
          confirmVariant="danger"
        />
      )}
    </div>
  );
}

function TileView({
  staff,
  onEdit,
  onDelete,
}: {
  staff: FacilityStaff[];
  onEdit: (s: FacilityStaff) => void;
  onDelete: (s: FacilityStaff) => void;
}) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      {staff.map((s) => (
        <div key={s.id} className="border border-gray-200 rounded-xl p-4 hover:shadow-sm transition-shadow">
          <div className="flex items-start justify-between mb-3">
            <p className="text-sm font-semibold text-gray-900 leading-snug">
              {s.firstname} {s.lastname}
            </p>
            <ActionMenu
              items={[
                { label: 'Edit Contact', icon: 'ri-edit-line', onClick: () => onEdit(s) },
                { label: 'Delete', icon: 'ri-delete-bin-line', onClick: () => onDelete(s), variant: 'danger', divider: true },
              ]}
            />
          </div>
          {s.email && (
            <div className="flex items-center gap-2 text-xs text-gray-500 mb-1.5">
              <i className="ri-mail-line text-gray-400 shrink-0" />
              <span className="truncate">{s.email}</span>
            </div>
          )}
          {s.phone && (
            <div className="flex items-center gap-2 text-xs text-gray-500 mb-1.5">
              <i className="ri-phone-line text-gray-400 shrink-0" />
              <span>{s.phone}</span>
            </div>
          )}
          <div className="border-t border-gray-100 mt-3 pt-3 grid grid-cols-2 gap-2">
            <div>
              <p className="text-xs text-gray-400">Active Cases</p>
              <p className="text-sm font-semibold text-gray-900">{s.activeCases ?? 0}</p>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

function ListView({
  staff,
  onEdit,
  onDelete,
}: {
  staff: FacilityStaff[];
  onEdit: (s: FacilityStaff) => void;
  onDelete: (s: FacilityStaff) => void;
}) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-gray-100">
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">First Name</th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">Last Name</th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">Email</th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">Phone</th>
            <th className="text-left px-3 py-2.5 text-xs font-medium text-gray-500">Active Cases</th>
            <th className="px-3 py-2.5" />
          </tr>
        </thead>
        <tbody>
          {staff.map((s) => (
            <tr key={s.id} className="border-b border-gray-50 hover:bg-gray-50/50">
              <td className="px-3 py-3 text-gray-900 font-medium">{s.firstname}</td>
              <td className="px-3 py-3 text-gray-700">{s.lastname}</td>
              <td className="px-3 py-3 text-gray-500">{s.email || '—'}</td>
              <td className="px-3 py-3 text-gray-500">{s.phone || '—'}</td>
              <td className="px-3 py-3 text-gray-700">{s.activeCases ?? 0}</td>
              <td className="px-3 py-3">
                <ActionMenu
                  items={[
                    { label: 'Edit Contact', icon: 'ri-edit-line', onClick: () => onEdit(s) },
                    { label: 'Delete', icon: 'ri-delete-bin-line', onClick: () => onDelete(s), variant: 'danger', divider: true },
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
