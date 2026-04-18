'use client';

import { useState, useEffect, useCallback } from 'react';
import { Modal } from '@/components/lien/modal';
import { tenantClientApi, ApiError } from '@/lib/tenant-client-api';
import { useToast } from '@/lib/toast-context';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

interface FormState {
  firstName: string;
  lastName:  string;
  email:     string;
  roleId:    string;
  password:  string;
}

interface FormErrors {
  firstName?: string;
  lastName?:  string;
  email?:     string;
  roleId?:    string;
  password?:  string;
}

const EMPTY_FORM: FormState = {
  firstName: '',
  lastName:  '',
  email:     '',
  roleId:    '',
  password:  '',
};

interface Role {
  id:   string;
  name: string;
}

interface AddUserModalProps {
  open:      boolean;
  tenantId:  string;
  onClose:   () => void;
  onSuccess: () => void;
}

function validate(form: FormState): FormErrors {
  const errors: FormErrors = {};
  if (!form.firstName.trim()) errors.firstName = 'First name is required.';
  if (!form.lastName.trim())  errors.lastName  = 'Last name is required.';
  if (!form.email.trim())           errors.email = 'Email is required.';
  else if (!EMAIL_RE.test(form.email.trim())) errors.email = 'Enter a valid email address.';
  if (!form.roleId)  errors.roleId  = 'Please select a role.';
  if (form.password.length < 8) errors.password = 'Password must be at least 8 characters.';
  return errors;
}

export function AddUserModal({ open, tenantId, onClose, onSuccess }: AddUserModalProps) {
  const { show: showToast } = useToast();

  const [form,     setForm]     = useState<FormState>(EMPTY_FORM);
  const [errors,   setErrors]   = useState<FormErrors>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const [loading,  setLoading]  = useState(false);

  const [roles,        setRoles]        = useState<Role[]>([]);
  const [rolesLoading, setRolesLoading] = useState(false);
  const [rolesError,   setRolesError]   = useState<string | null>(null);

  const fetchRoles = useCallback(async () => {
    setRolesLoading(true);
    setRolesError(null);
    try {
      const { data } = await tenantClientApi.getRoles();
      setRoles(data ?? []);
    } catch {
      setRolesError('Unable to load roles right now.');
    } finally {
      setRolesLoading(false);
    }
  }, []);

  useEffect(() => {
    if (open) {
      setForm(EMPTY_FORM);
      setErrors({});
      setApiError(null);
      fetchRoles();
    }
  }, [open, fetchRoles]);

  function handleField<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm(prev => ({ ...prev, [key]: value }));
    if (errors[key]) setErrors(prev => ({ ...prev, [key]: undefined }));
    if (apiError)    setApiError(null);
  }

  async function handleSubmit() {
    const errs = validate(form);
    if (Object.keys(errs).length > 0) {
      setErrors(errs);
      return;
    }

    setLoading(true);
    setApiError(null);

    try {
      await tenantClientApi.createUser({
        tenantId,
        firstName: form.firstName.trim(),
        lastName:  form.lastName.trim(),
        email:     form.email.trim(),
        password:  form.password,
        roleIds:   form.roleId ? [form.roleId] : undefined,
      });

      showToast('User created successfully.', 'success');
      onSuccess();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isConflict || err.status === 400) {
          const msg = err.message.toLowerCase();
          setApiError(
            msg.includes('email') || msg.includes('exist') || msg.includes('duplicate')
              ? 'A user with this email already exists.'
              : err.message,
          );
        } else if (err.isForbidden) {
          setApiError('You do not have permission to create users in this tenant.');
        } else {
          setApiError('Something went wrong. Please try again.');
        }
      } else {
        setApiError('Something went wrong. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  }

  const hasErrors = Object.keys(errors).length > 0;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Add User"
      subtitle="Create a new user in this tenant."
      size="md"
      footer={
        <>
          <button
            onClick={onClose}
            disabled={loading}
            className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={loading || rolesLoading}
            className="text-sm px-4 py-2 bg-primary hover:bg-primary/90 text-white rounded-lg disabled:opacity-50 flex items-center gap-2"
          >
            {loading && <i className="ri-loader-4-line animate-spin text-base" />}
            {loading ? 'Creating...' : 'Create User'}
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

        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-1">
            <label className="block text-xs font-medium text-gray-700">
              First Name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={form.firstName}
              onChange={e => handleField('firstName', e.target.value)}
              autoComplete="given-name"
              className={`w-full rounded-md border py-2 px-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary ${errors.firstName ? 'border-red-400 bg-red-50' : 'border-gray-300 bg-white'}`}
              placeholder="Jane"
            />
            {errors.firstName && <p className="text-xs text-red-600">{errors.firstName}</p>}
          </div>

          <div className="space-y-1">
            <label className="block text-xs font-medium text-gray-700">
              Last Name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={form.lastName}
              onChange={e => handleField('lastName', e.target.value)}
              autoComplete="family-name"
              className={`w-full rounded-md border py-2 px-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary ${errors.lastName ? 'border-red-400 bg-red-50' : 'border-gray-300 bg-white'}`}
              placeholder="Doe"
            />
            {errors.lastName && <p className="text-xs text-red-600">{errors.lastName}</p>}
          </div>
        </div>

        <div className="space-y-1">
          <label className="block text-xs font-medium text-gray-700">
            Email <span className="text-red-500">*</span>
          </label>
          <input
            type="email"
            value={form.email}
            onChange={e => handleField('email', e.target.value)}
            autoComplete="email"
            className={`w-full rounded-md border py-2 px-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary ${errors.email ? 'border-red-400 bg-red-50' : 'border-gray-300 bg-white'}`}
            placeholder="jane.doe@company.com"
          />
          {errors.email && <p className="text-xs text-red-600">{errors.email}</p>}
        </div>

        <div className="space-y-1">
          <label className="block text-xs font-medium text-gray-700">
            Role <span className="text-red-500">*</span>
          </label>
          {rolesError ? (
            <p className="text-xs text-red-600">{rolesError}</p>
          ) : (
            <select
              value={form.roleId}
              onChange={e => handleField('roleId', e.target.value)}
              disabled={rolesLoading}
              className={`w-full rounded-md border py-2 px-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary disabled:bg-gray-50 disabled:text-gray-400 ${errors.roleId ? 'border-red-400 bg-red-50' : 'border-gray-300 bg-white'}`}
            >
              <option value="">
                {rolesLoading ? 'Loading roles...' : '— Select a role —'}
              </option>
              {roles.map(r => (
                <option key={r.id} value={r.id}>{r.name}</option>
              ))}
            </select>
          )}
          {errors.roleId && <p className="text-xs text-red-600">{errors.roleId}</p>}
          {!rolesLoading && !rolesError && roles.length === 0 && (
            <p className="text-xs text-gray-400">No roles available.</p>
          )}
        </div>

        <div className="space-y-1">
          <label className="block text-xs font-medium text-gray-700">
            Temporary Password <span className="text-red-500">*</span>
          </label>
          <input
            type="password"
            value={form.password}
            onChange={e => handleField('password', e.target.value)}
            autoComplete="new-password"
            className={`w-full rounded-md border py-2 px-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary ${errors.password ? 'border-red-400 bg-red-50' : 'border-gray-300 bg-white'}`}
            placeholder="Min. 8 characters"
          />
          {errors.password && <p className="text-xs text-red-600">{errors.password}</p>}
          <p className="text-[11px] text-gray-400">
            The user will need to be informed of this password to log in.
          </p>
        </div>

        {hasErrors && !Object.values(errors).every(Boolean) === false && (
          <p className="text-xs text-gray-400">Fields marked with * are required.</p>
        )}
      </div>
    </Modal>
  );
}
