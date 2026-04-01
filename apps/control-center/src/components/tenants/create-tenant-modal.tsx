'use client';

import { useState, useRef, useId, useEffect } from 'react';
import { useRouter }                           from 'next/navigation';
import { createTenantAction }                  from '@/app/tenants/actions';
import type { CreateTenantResult }             from '@/app/tenants/actions';

interface CreateTenantModalProps {
  onClose: () => void;
}

type Step = 'form' | 'success';

export function CreateTenantModal({ onClose }: CreateTenantModalProps) {
  const titleId = useId();
  const router  = useRouter();

  const [step, setStep]         = useState<Step>('form');
  const [isPending, setIsPending] = useState(false);
  const [error, setError]       = useState<string | null>(null);
  const [result, setResult]     = useState<NonNullable<CreateTenantResult['adminUser']> & NonNullable<CreateTenantResult['tenant']> | null>(null);
  const [copied, setCopied]     = useState(false);

  const firstInputRef = useRef<HTMLInputElement>(null);

  const [form, setForm] = useState({
    name:           '',
    code:           '',
    orgType:        'LAW_FIRM',
    adminEmail:     '',
    adminFirstName: '',
    adminLastName:  '',
  });

  useEffect(() => {
    firstInputRef.current?.focus();
  }, []);

  useEffect(() => {
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape' && !isPending) onClose();
    }
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [onClose, isPending]);

  function deriveCode(name: string) {
    return name
      .toUpperCase()
      .replace(/[^A-Z0-9]/g, '')
      .slice(0, 8);
  }

  function handleNameChange(e: React.ChangeEvent<HTMLInputElement>) {
    const name = e.target.value;
    setForm(f => ({
      ...f,
      name,
      code: f.code === deriveCode(f.name) ? deriveCode(name) : f.code,
    }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setIsPending(true);

    try {
      const res = await createTenantAction(form);
      if (!res.success || !res.tenant || !res.adminUser) {
        setError(res.error ?? 'Something went wrong.');
        return;
      }
      setResult({ ...res.tenant, ...res.adminUser });
      setStep('success');
      router.refresh();
    } finally {
      setIsPending(false);
    }
  }

  async function handleCopy() {
    if (!result) return;
    await navigator.clipboard.writeText(result.temporaryPassword);
    setCopied(true);
    setTimeout(() => setCopied(false), 2500);
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-[2px]"
        aria-hidden="true"
        onClick={() => !isPending && onClose()}
      />

      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="relative z-10 w-full max-w-lg mx-4 bg-white rounded-xl shadow-xl border border-gray-200"
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h2 id={titleId} className="text-sm font-semibold text-gray-900">
            {step === 'form' ? 'Create Tenant' : 'Tenant Created'}
          </h2>
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="text-gray-400 hover:text-gray-600 transition-colors disabled:opacity-40"
            aria-label="Close"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Form step */}
        {step === 'form' && (
          <form onSubmit={handleSubmit} className="px-6 py-5 space-y-5">
            {/* Tenant info */}
            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                Tenant Information
              </legend>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Tenant Name <span className="text-red-500">*</span>
                </label>
                <input
                  ref={firstInputRef}
                  type="text"
                  required
                  maxLength={120}
                  value={form.name}
                  onChange={handleNameChange}
                  placeholder="e.g. Acme Law Group"
                  className={inputClass}
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Short Code <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  maxLength={12}
                  pattern="[A-Za-z0-9]{2,12}"
                  value={form.code}
                  onChange={e => setForm(f => ({ ...f, code: e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, '') }))}
                  placeholder="e.g. ACMELG"
                  className={`${inputClass} font-mono tracking-widest`}
                />
                <p className="mt-1 text-[11px] text-gray-400">
                  2–12 alphanumeric characters. Used as a unique identifier — cannot be changed later.
                </p>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Organization Type <span className="text-red-500">*</span>
                </label>
                <select
                  value={form.orgType}
                  onChange={e => setForm(f => ({ ...f, orgType: e.target.value }))}
                  className={selectClass}
                >
                  <option value="LAW_FIRM">Law Firm</option>
                  <option value="PROVIDER">Provider</option>
                  <option value="FUNDER">Funder</option>
                  <option value="LIEN_OWNER">Lien Owner</option>
                </select>
                <p className="mt-1 text-[11px] text-gray-400">
                  Determines what the tenant can do on the platform.
                </p>
              </div>
            </fieldset>

            {/* Divider */}
            <div className="border-t border-gray-100" />

            {/* Admin user */}
            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                Default Admin User
              </legend>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    First Name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    maxLength={80}
                    value={form.adminFirstName}
                    onChange={e => setForm(f => ({ ...f, adminFirstName: e.target.value }))}
                    placeholder="Jane"
                    className={inputClass}
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Last Name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    maxLength={80}
                    value={form.adminLastName}
                    onChange={e => setForm(f => ({ ...f, adminLastName: e.target.value }))}
                    placeholder="Smith"
                    className={inputClass}
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Email Address <span className="text-red-500">*</span>
                </label>
                <input
                  type="email"
                  required
                  maxLength={200}
                  value={form.adminEmail}
                  onChange={e => setForm(f => ({ ...f, adminEmail: e.target.value }))}
                  placeholder="jane.smith@acme.com"
                  className={inputClass}
                />
              </div>
            </fieldset>

            {/* Error */}
            {error && (
              <div className="rounded-md bg-red-50 border border-red-200 px-3 py-2.5 text-xs text-red-700">
                {error}
              </div>
            )}

            {/* Actions */}
            <div className="flex items-center justify-end gap-2 pt-1">
              <button
                type="button"
                onClick={onClose}
                disabled={isPending}
                className="px-3 py-1.5 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 transition-colors disabled:opacity-40"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isPending}
                className="px-4 py-1.5 text-sm font-medium text-white bg-indigo-600 rounded-md hover:bg-indigo-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1"
              >
                {isPending ? (
                  <span className="flex items-center gap-1.5">
                    <span className="h-3.5 w-3.5 rounded-full border-2 border-white/60 border-t-transparent animate-spin" aria-hidden="true" />
                    Creating…
                  </span>
                ) : (
                  'Create Tenant'
                )}
              </button>
            </div>
          </form>
        )}

        {/* Success step */}
        {step === 'success' && result && (
          <div className="px-6 py-5 space-y-5">
            {/* Success banner */}
            <div className="flex items-start gap-3 rounded-md bg-green-50 border border-green-200 px-4 py-3">
              <svg className="h-4 w-4 text-green-600 mt-0.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
              </svg>
              <div className="text-xs text-green-800">
                <p className="font-semibold">Tenant created successfully</p>
                <p className="mt-0.5 text-green-700">
                  <span className="font-mono bg-green-100 px-1 rounded">{result.code}</span>
                  {' '}— {result.displayName}
                </p>
              </div>
            </div>

            {/* Temp password notice */}
            <div className="space-y-2">
              <p className="text-xs font-medium text-gray-700">
                Temporary password for <span className="font-mono text-gray-900">{result.adminEmail}</span>
              </p>
              <p className="text-[11px] text-amber-700 bg-amber-50 border border-amber-200 rounded px-3 py-2">
                This password is shown <strong>once only</strong>. Copy it now and share it securely with the admin user — they should change it on first login.
              </p>
              <div className="flex items-center gap-2">
                <code className="flex-1 font-mono text-sm bg-gray-100 border border-gray-200 rounded-md px-3 py-2 text-gray-900 tracking-widest select-all">
                  {result.temporaryPassword}
                </code>
                <button
                  type="button"
                  onClick={handleCopy}
                  className={[
                    'shrink-0 px-3 py-2 text-xs font-medium rounded-md border transition-colors',
                    copied
                      ? 'bg-green-50 border-green-300 text-green-700'
                      : 'bg-white border-gray-300 text-gray-700 hover:bg-gray-50',
                  ].join(' ')}
                >
                  {copied ? 'Copied!' : 'Copy'}
                </button>
              </div>
            </div>

            {/* Close */}
            <div className="flex justify-end pt-1">
              <button
                type="button"
                onClick={onClose}
                className="px-4 py-1.5 text-sm font-medium text-white bg-indigo-600 rounded-md hover:bg-indigo-700 transition-colors"
              >
                Done
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

const inputClass = [
  'w-full text-sm border border-gray-200 rounded-md px-3 py-1.5',
  'text-gray-900 placeholder-gray-400',
  'focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400',
].join(' ');

const selectClass = [
  'w-full text-sm border border-gray-200 rounded-md px-3 py-1.5 bg-white',
  'text-gray-900',
  'focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400',
].join(' ');
