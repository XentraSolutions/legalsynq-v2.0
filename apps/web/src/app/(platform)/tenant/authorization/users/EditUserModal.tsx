'use client';

import { useState, useEffect, useCallback } from 'react';
import { Modal } from '@/components/lien/modal';
import { tenantClientApi, ApiError } from '@/lib/tenant-client-api';
import { useToast } from '@/lib/toast-context';
import type { TenantUser } from '@/types/tenant';

interface Role {
  id:   string;
  name: string;
}

interface EditUserModalProps {
  open:      boolean;
  user:      TenantUser;
  onClose:   () => void;
  onSuccess: () => void;
}

function ReadOnlyField({ label, value }: { label: string; value: string }) {
  return (
    <div className="space-y-1">
      <p className="text-xs font-medium text-gray-500">{label}</p>
      <p className="text-sm text-gray-700 py-2 px-3 rounded-md bg-gray-50 border border-gray-200 min-h-[38px]">
        {value || <span className="text-gray-400">—</span>}
      </p>
    </div>
  );
}

export function EditUserModal({ open, user, onClose, onSuccess }: EditUserModalProps) {
  const { show: showToast } = useToast();

  const [roles,        setRoles]        = useState<Role[]>([]);
  const [roleId,       setRoleId]       = useState('');
  const [phone,        setPhone]        = useState('');
  const [initialRoleId, setInitialRoleId] = useState('');
  const [initialPhone,  setInitialPhone]  = useState('');

  const [detailLoading, setDetailLoading] = useState(false);
  const [rolesLoading,  setRolesLoading]  = useState(false);
  const [submitting,    setSubmitting]    = useState(false);
  const [apiError,      setApiError]      = useState<string | null>(null);
  const [roleError,     setRoleError]     = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setDetailLoading(true);
    setRolesLoading(true);
    setApiError(null);
    setRoleError(null);

    const [detailResult, rolesResult] = await Promise.allSettled([
      tenantClientApi.getUserDetail(user.id),
      tenantClientApi.getRoles(),
    ]);

    if (detailResult.status === 'fulfilled') {
      const detail = detailResult.value.data;
      const currentRoleId = detail.roles[0]?.roleId ?? '';
      const currentPhone  = detail.phone ?? '';
      setRoleId(currentRoleId);
      setInitialRoleId(currentRoleId);
      setPhone(currentPhone);
      setInitialPhone(currentPhone);
    } else {
      setApiError('Unable to load user details. Please try again.');
    }
    setDetailLoading(false);

    if (rolesResult.status === 'fulfilled') {
      setRoles(rolesResult.value.data ?? []);
    }
    setRolesLoading(false);
  }, [user.id]);

  useEffect(() => {
    if (open) {
      setRoleId('');
      setPhone('');
      setInitialRoleId('');
      setInitialPhone('');
      setRoles([]);
      setApiError(null);
      setRoleError(null);
      loadData();
    }
  }, [open, loadData]);

  async function handleSave() {
    if (!roleId) {
      setRoleError('Please select a role.');
      return;
    }

    const roleChanged  = roleId  !== initialRoleId;
    const phoneChanged = phone.trim() !== initialPhone;

    if (!roleChanged && !phoneChanged) {
      onClose();
      return;
    }

    setSubmitting(true);
    setApiError(null);

    try {
      if (roleChanged) {
        const detail = await tenantClientApi.getUserDetail(user.id);
        const currentRoleIds = detail.data.roles.map(r => r.roleId);
        if (currentRoleIds.length > 0) {
          await Promise.all(currentRoleIds.map(rid => tenantClientApi.removeRole(user.id, rid)));
        }
        if (roleId) {
          await tenantClientApi.assignRole(user.id, roleId);
        }
      }

      if (phoneChanged) {
        await tenantClientApi.updatePhone(user.id, phone.trim() || null);
      }

      showToast('User updated successfully.', 'success');
      onSuccess();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isForbidden) {
          setApiError('You do not have permission to edit this user.');
        } else if (err.status === 400) {
          setApiError(err.message || 'Invalid data. Please check your input.');
        } else {
          setApiError('Something went wrong. Please try again.');
        }
      } else {
        setApiError('Something went wrong. Please try again.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  const loading = detailLoading || rolesLoading;
  const displayName = [user.firstName, user.lastName].filter(Boolean).join(' ') || user.email || 'Unknown User';

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Edit User"
      subtitle={displayName}
      size="md"
      footer={
        <>
          <button
            onClick={onClose}
            disabled={submitting}
            className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            disabled={loading || submitting}
            className="text-sm px-4 py-2 bg-primary hover:bg-primary/90 text-white rounded-lg disabled:opacity-50 flex items-center gap-2"
          >
            {submitting && <i className="ri-loader-4-line animate-spin text-base" />}
            {submitting ? 'Saving...' : 'Save Changes'}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        {apiError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-700 flex items-start gap-2">
            <i className="ri-error-warning-line text-base mt-0.5 shrink-0" />
            <span>{apiError}</span>
          </div>
        )}

        {loading ? (
          <div className="flex items-center justify-center py-10">
            <i className="ri-loader-4-line animate-spin text-2xl text-gray-300" />
          </div>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-4">
              <ReadOnlyField label="First Name" value={(user.firstName ?? '').trim()} />
              <ReadOnlyField label="Last Name"  value={(user.lastName  ?? '').trim()} />
            </div>

            <ReadOnlyField label="Email" value={(user.email ?? '').trim()} />

            <div className="space-y-1">
              <label className="block text-xs font-medium text-gray-700">
                Role <span className="text-red-500">*</span>
              </label>
              <select
                value={roleId}
                onChange={e => { setRoleId(e.target.value); setRoleError(null); }}
                disabled={rolesLoading}
                className={`w-full rounded-md border py-2 px-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary disabled:bg-gray-50 ${roleError ? 'border-red-400 bg-red-50' : 'border-gray-300 bg-white'}`}
              >
                <option value="">— Select a role —</option>
                {roles.map(r => (
                  <option key={r.id} value={r.id}>{r.name}</option>
                ))}
              </select>
              {roleError && <p className="text-xs text-red-600">{roleError}</p>}
            </div>

            <div className="space-y-1">
              <label className="block text-xs font-medium text-gray-700">Phone</label>
              <input
                type="tel"
                value={phone}
                onChange={e => setPhone(e.target.value)}
                placeholder="+1 555 000 0000"
                autoComplete="tel"
                className="w-full rounded-md border border-gray-300 bg-white py-2 px-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary"
              />
              <p className="text-[11px] text-gray-400">Optional. Must be in E.164 format (e.g. +15551234567).</p>
            </div>

            <p className="text-[11px] text-gray-400 pt-1">
              Name and email cannot be changed here. Contact your platform administrator for profile updates.
            </p>
          </>
        )}
      </div>
    </Modal>
  );
}
