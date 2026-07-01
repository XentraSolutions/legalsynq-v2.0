'use client';

import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { createTenantAction } from '@/app/tenants/actions';
import type { CreateTenantResult } from '@/app/tenants/actions';

interface AddressSuggestion {
  displayName: string;
  addressLine1: string;
  city: string;
  state: string;
  postalCode: string;
  latitude: number;
  longitude: number;
}

interface CreateTenantModalProps {
  onClose: () => void;
  portalBaseDomain?: string;
}

type Step = 'form' | 'provisioning' | 'success';
type ResultState = NonNullable<CreateTenantResult['adminUser']> & NonNullable<CreateTenantResult['tenant']>;

const FLOW_STAGES = ['Tenant', 'Owner', 'Workspace', 'Provision', 'Done'] as const;
const PROVISIONING_STEPS = [
  'Creating tenant',
  'Creating owner account',
  'Assigning owner membership',
  'Creating default roles & permissions',
  'Applying default configuration',
  'Preparing workspace home',
  'Finalizing',
] as const;
const PROVISIONING_ADVANCE_MS = [500, 900, 1300, 1800, 2300, 2900];
const MIN_PROVISIONING_DURATION_MS = 2400;

export function CreateTenantModal({ onClose, portalBaseDomain }: CreateTenantModalProps) {
  const titleId = useId();
  const router = useRouter();

  const [step, setStep] = useState<Step>('form');
  const [isPending, setIsPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ResultState | null>(null);
  const [copied, setCopied] = useState(false);
  const [provisioningIndex, setProvisioningIndex] = useState(0);
  const [provisioningRunId, setProvisioningRunId] = useState(0);

  const firstInputRef = useRef<HTMLInputElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const addrInputRef = useRef<HTMLInputElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const progressionTimeoutsRef = useRef<number[]>([]);
  const copyTimeoutRef = useRef<number | null>(null);

  const [form, setForm] = useState({
    name: '',
    code: '',
    orgType: 'LAW_FIRM',
    adminEmail: '',
    adminFirstName: '',
    adminLastName: '',
  });

  const [address, setAddress] = useState({
    raw: '',
    addressLine1: '',
    city: '',
    state: '',
    postalCode: '',
    latitude: null as number | null,
    longitude: null as number | null,
  });

  const [suggestions, setSuggestions] = useState<AddressSuggestion[]>([]);
  const [addrLoading, setAddrLoading] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const [selectedIndex, setSelectedIndex] = useState(-1);

  const clearProgressionTimers = useCallback(() => {
    progressionTimeoutsRef.current.forEach(window.clearTimeout);
    progressionTimeoutsRef.current = [];
  }, []);

  useEffect(() => {
    if (step === 'form') firstInputRef.current?.focus();
  }, [step]);

  useEffect(() => {
    function handleKey(event: KeyboardEvent) {
      if (event.key === 'Escape' && !isPending) onClose();
    }

    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [isPending, onClose]);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(event.target as Node) &&
        addrInputRef.current &&
        !addrInputRef.current.contains(event.target as Node)
      ) {
        setShowDropdown(false);
      }
    }

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  useEffect(() => {
    if (step !== 'provisioning' || !isPending) {
      clearProgressionTimers();
      return;
    }

    clearProgressionTimers();
    PROVISIONING_ADVANCE_MS.forEach((delay, index) => {
      const timeoutId = window.setTimeout(() => {
        setProvisioningIndex(Math.min(index + 1, PROVISIONING_STEPS.length - 2));
      }, delay);
      progressionTimeoutsRef.current.push(timeoutId);
    });

    return clearProgressionTimers;
  }, [clearProgressionTimers, isPending, provisioningRunId, step]);

  useEffect(() => () => {
    clearProgressionTimers();
    if (copyTimeoutRef.current !== null) window.clearTimeout(copyTimeoutRef.current);
    if (debounceRef.current) clearTimeout(debounceRef.current);
  }, [clearProgressionTimers]);

  function deriveCode(name: string) {
    return name
      .toLowerCase()
      .trim()
      .replace(/[^a-z0-9\s-]/g, '')
      .replace(/[\s_]+/g, '-')
      .replace(/-{2,}/g, '-')
      .replace(/^-|-$/g, '')
      .slice(0, 63);
  }

  function handleNameChange(event: React.ChangeEvent<HTMLInputElement>) {
    const name = event.target.value;
    setForm(current => ({
      ...current,
      name,
      code: current.code === deriveCode(current.name) ? deriveCode(name) : current.code,
    }));
  }

  const fetchSuggestions = useCallback(async (query: string) => {
    if (query.trim().length < 3) {
      setSuggestions([]);
      setShowDropdown(false);
      return;
    }

    setAddrLoading(true);
    try {
      const response = await fetch(`/api/geocode/address?q=${encodeURIComponent(query)}`);
      if (!response.ok) return;
      const data: AddressSuggestion[] = await response.json();
      setSuggestions(data);
      setShowDropdown(data.length > 0);
      setSelectedIndex(-1);
    } catch {
      setSuggestions([]);
    } finally {
      setAddrLoading(false);
    }
  }, []);

  function handleAddressInput(event: React.ChangeEvent<HTMLInputElement>) {
    const value = event.target.value;
    setAddress(current => ({
      ...current,
      raw: value,
      addressLine1: '',
      city: '',
      state: '',
      postalCode: '',
      latitude: null,
      longitude: null,
    }));

    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => fetchSuggestions(value), 300);
  }

  function handleAddressKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (!showDropdown || suggestions.length === 0) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setSelectedIndex(index => Math.min(index + 1, suggestions.length - 1));
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      setSelectedIndex(index => Math.max(index - 1, 0));
      return;
    }

    if (event.key === 'Enter' && selectedIndex >= 0) {
      event.preventDefault();
      selectSuggestion(suggestions[selectedIndex]);
      return;
    }

    if (event.key === 'Escape') setShowDropdown(false);
  }

  function selectSuggestion(suggestion: AddressSuggestion) {
    const typedLeading = address.raw.trim().match(/^(\d+[-\w]*)\s+/);
    const suggestionHasNumber = /^\d/.test(suggestion.addressLine1);
    const addressLine1 =
      typedLeading && !suggestionHasNumber
        ? `${typedLeading[1]} ${suggestion.addressLine1}`
        : suggestion.addressLine1;

    const displayName = [
      addressLine1,
      suggestion.city,
      suggestion.postalCode ? `${suggestion.state} ${suggestion.postalCode}` : suggestion.state,
    ].filter(Boolean).join(', ');

    setAddress({
      raw: displayName,
      addressLine1,
      city: suggestion.city,
      state: suggestion.state,
      postalCode: suggestion.postalCode,
      latitude: suggestion.latitude,
      longitude: suggestion.longitude,
    });
    setSuggestions([]);
    setShowDropdown(false);
  }

  function buildPayload() {
    return {
      ...form,
      ...(address.addressLine1 ? {
        addressLine1: address.addressLine1,
        city: address.city,
        state: address.state,
        postalCode: address.postalCode,
        latitude: address.latitude ?? undefined,
        longitude: address.longitude ?? undefined,
        geoPointSource: 'nominatim',
      } : {}),
    };
  }

  async function submitTenant() {
    setCopied(false);
    setError(null);
    setResult(null);
    setProvisioningIndex(0);
    setProvisioningRunId(current => current + 1);
    setStep('provisioning');
    setIsPending(true);

    const startedAt = Date.now();

    try {
      const response = await createTenantAction(buildPayload());
      const remainingDelay = Math.max(0, MIN_PROVISIONING_DURATION_MS - (Date.now() - startedAt));
      if (remainingDelay > 0) await delay(remainingDelay);

      if (!response.success || !response.tenant || !response.adminUser) {
        setError(response.error ?? 'Something went wrong. Please try again.');
        return;
      }

      setProvisioningIndex(PROVISIONING_STEPS.length - 1);
      setResult({ ...response.tenant, ...response.adminUser });
      setStep('success');
      router.refresh();
    } catch (err) {
      if (err && typeof err === 'object' && 'digest' in err) throw err;
      setError(err instanceof Error ? err.message : 'An unexpected error occurred. Please try again.');
    } finally {
      setIsPending(false);
    }
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    await submitTenant();
  }

  async function handleCopy() {
    if (!result?.temporaryPassword) return;
    await navigator.clipboard.writeText(result.temporaryPassword);
    setCopied(true);
    if (copyTimeoutRef.current !== null) window.clearTimeout(copyTimeoutRef.current);
    copyTimeoutRef.current = window.setTimeout(() => setCopied(false), 2500);
  }

  const previewFqdn = form.code ? buildTenantHostname(form.code, portalBaseDomain) : null;
  const workspaceUrl = result?.workspaceUrl ?? result?.hostname ?? previewFqdn;

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
        className="relative z-10 w-full max-w-2xl mx-4 bg-white rounded-xl shadow-xl border border-gray-200 max-h-[90vh] overflow-y-auto"
      >
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 sticky top-0 bg-white z-10">
          <h2 id={titleId} className="text-sm font-semibold text-gray-900">
            {step === 'form' ? 'Create Tenant' : step === 'provisioning' ? 'Provision Workspace' : 'Workspace Ready'}
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

        {step === 'form' && (
          <form onSubmit={handleSubmit} className="px-6 py-5 space-y-5">
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
                  Tenant Code <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  required
                  minLength={2}
                  maxLength={63}
                  pattern="[a-z0-9]([a-z0-9\\-]{0,61}[a-z0-9])?"
                  value={form.code}
                  onChange={event => setForm(current => ({
                    ...current,
                    code: event.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '').replace(/^-/, ''),
                  }))}
                  placeholder="e.g. acme-law"
                  className={`${inputClass} font-mono`}
                />
                <p className="mt-1 text-[11px] text-gray-400">
                  Lowercase letters, numbers, and hyphens. This becomes the tenant key and workspace host
                  {' '}
                  <span className="font-mono">
                    {previewFqdn ?? `${form.code || '...'}.${portalBaseDomain || 'legalsynq.com'}`}
                  </span>.
                </p>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Organization Type <span className="text-red-500">*</span>
                </label>
                <select
                  value={form.orgType}
                  onChange={event => setForm(current => ({ ...current, orgType: event.target.value }))}
                  className={selectClass}
                >
                  <option value="LAW_FIRM">Law Firm</option>
                  <option value="PROVIDER">Provider</option>
                  <option value="FUNDER">Funder</option>
                  <option value="LIEN_OWNER">Lien Owner</option>
                </select>
                <p className="mt-1 text-[11px] text-gray-400">
                  Determines the default platform setup for this workspace.
                </p>
              </div>
            </fieldset>

            <div className="border-t border-gray-100" />

            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                Address <span className="text-gray-400 font-normal normal-case">(optional)</span>
              </legend>

              <div className="relative">
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Street Address
                </label>
                <div className="relative">
                  <input
                    ref={addrInputRef}
                    type="text"
                    autoComplete="off"
                    value={address.raw}
                    onChange={handleAddressInput}
                    onKeyDown={handleAddressKeyDown}
                    onFocus={() => suggestions.length > 0 && setShowDropdown(true)}
                    placeholder="e.g. 123 Main Street…"
                    className={inputClass}
                  />
                  {addrLoading && (
                    <span className="absolute right-2 top-1/2 -translate-y-1/2">
                      <span className="h-3.5 w-3.5 rounded-full border-2 border-gray-300 border-t-indigo-500 animate-spin block" />
                    </span>
                  )}
                  {address.latitude !== null && (
                    <span className="absolute right-2 top-1/2 -translate-y-1/2 text-green-500" title="Coordinates captured">
                      <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                      </svg>
                    </span>
                  )}
                </div>

                {showDropdown && suggestions.length > 0 && (
                  <div
                    ref={dropdownRef}
                    className="absolute z-20 left-0 right-0 mt-1 bg-white border border-gray-200 rounded-md shadow-lg overflow-hidden"
                  >
                    {suggestions.map((suggestion, index) => (
                      <button
                        key={suggestion.displayName}
                        type="button"
                        onMouseDown={event => { event.preventDefault(); selectSuggestion(suggestion); }}
                        className={[
                          'w-full text-left px-3 py-2 text-xs hover:bg-indigo-50 transition-colors',
                          index === selectedIndex ? 'bg-indigo-50 text-indigo-900' : 'text-gray-800',
                          index > 0 ? 'border-t border-gray-100' : '',
                        ].join(' ')}
                      >
                        <span className="font-medium">{suggestion.addressLine1}</span>
                        <span className="text-gray-500 ml-1">{suggestion.city}, {suggestion.state} {suggestion.postalCode}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {address.addressLine1 && (
                <div className="grid grid-cols-3 gap-2">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">City</label>
                    <input
                      type="text"
                      value={address.city}
                      onChange={event => setAddress(current => ({ ...current, city: event.target.value }))}
                      className={inputClass}
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">State</label>
                    <input
                      type="text"
                      maxLength={2}
                      value={address.state}
                      onChange={event => setAddress(current => ({ ...current, state: event.target.value.toUpperCase() }))}
                      className={`${inputClass} font-mono uppercase`}
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">ZIP</label>
                    <input
                      type="text"
                      maxLength={10}
                      value={address.postalCode}
                      onChange={event => setAddress(current => ({ ...current, postalCode: event.target.value }))}
                      className={`${inputClass} font-mono`}
                    />
                  </div>
                </div>
              )}
            </fieldset>

            <div className="border-t border-gray-100" />

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
                    onChange={event => setForm(current => ({ ...current, adminFirstName: event.target.value }))}
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
                    onChange={event => setForm(current => ({ ...current, adminLastName: event.target.value }))}
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
                  onChange={event => setForm(current => ({ ...current, adminEmail: event.target.value }))}
                  placeholder="jane.smith@acme.com"
                  className={inputClass}
                />
              </div>
            </fieldset>

            {error && (
              <div className="rounded-md bg-red-50 border border-red-200 px-3 py-2.5 text-xs text-red-700">
                {error}
              </div>
            )}

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
                Create Tenant
              </button>
            </div>
          </form>
        )}

        {step === 'provisioning' && (
          <div className="px-6 py-5 space-y-5">
            <ProvisioningFlowStepper currentStage={3} />

            <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
              <div className="px-6 py-6 sm:px-7 space-y-6">
                <div className="space-y-2">
                  <h3 className="text-2xl font-semibold text-gray-900">Setting up your workspace</h3>
                  <p className="text-sm text-gray-500">
                    This usually takes a few moments. Please keep this modal open while the workspace is provisioned.
                  </p>
                </div>

                <div className="space-y-3">
                  {PROVISIONING_STEPS.map((label, index) => {
                    const isComplete = !error && (index < provisioningIndex || (!isPending && !!result));
                    const isCurrent = !error && isPending && index === provisioningIndex;
                    const isFailed = !!error && index === provisioningIndex;

                    return (
                      <div key={label} className="flex items-center gap-3">
                        <ProvisioningStatusIcon
                          stepNumber={index + 1}
                          isComplete={isComplete}
                          isCurrent={isCurrent}
                          isFailed={isFailed}
                        />
                        <span className={[
                          'text-sm',
                          isComplete || isCurrent ? 'text-gray-900' : 'text-gray-400',
                          isCurrent ? 'font-medium' : '',
                          isFailed ? 'text-red-700 font-medium' : '',
                        ].join(' ')}>
                          {label}
                        </span>
                      </div>
                    );
                  })}
                </div>

                <div className="rounded-lg border border-gray-100 bg-gray-50 px-4 py-3">
                  <p className="text-xs font-medium text-gray-700">Workspace host</p>
                  <p className="mt-1 font-mono text-sm text-gray-900">
                    {previewFqdn ?? `${form.code || '...'}.${portalBaseDomain || 'legalsynq.com'}`}
                  </p>
                </div>
              </div>
            </div>

            {error ? (
              <div className="space-y-4">
                <div className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                  {error}
                </div>
                <div className="flex items-center justify-end gap-2">
                  <button
                    type="button"
                    onClick={onClose}
                    className="px-3 py-1.5 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 transition-colors"
                  >
                    Close
                  </button>
                  <button
                    type="button"
                    onClick={() => { void submitTenant(); }}
                    className="px-4 py-1.5 text-sm font-medium text-white bg-indigo-600 rounded-md hover:bg-indigo-700 transition-colors"
                  >
                    Retry
                  </button>
                </div>
              </div>
            ) : (
              <p className="text-xs text-gray-500">
                Owner credentials and the canonical workspace URL will appear here as soon as setup completes.
              </p>
            )}
          </div>
        )}

        {step === 'success' && result && (
          <div className="px-6 py-5 space-y-5">
            <ProvisioningFlowStepper currentStage={4} />

            <div className="flex items-start gap-3 rounded-md bg-green-50 border border-green-200 px-4 py-3">
              <svg className="h-4 w-4 text-green-600 mt-0.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
              </svg>
              <div className="text-sm text-green-800">
                <p className="font-semibold">Workspace provisioned</p>
                <p className="mt-1 text-green-700">
                  <span className="font-mono bg-green-100 px-1 rounded">{result.tenantKey}</span>
                  {' '}— {result.displayName}
                </p>
              </div>
            </div>

            <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
              <div className="grid gap-4 px-5 py-5 sm:grid-cols-2">
                <SummaryItem label="Tenant Key" value={result.tenantKey} mono />
                <SummaryItem label="Subdomain" value={result.subdomain ?? result.tenantKey} mono />
                <SummaryItem
                  label="Workspace URL"
                  value={workspaceUrl ?? `${result.tenantKey}.${portalBaseDomain || 'legalsynq.com'}`}
                  mono
                  href={workspaceUrl ? `https://${workspaceUrl}` : undefined}
                />
                <SummaryItem label="Status" value={result.provisioningStatus ?? 'Provisioned'} />
              </div>
            </div>

            <div className="space-y-2">
              <p className="text-xs font-medium text-gray-700">
                Temporary password for <span className="font-mono text-gray-900">{result.adminEmail}</span>
              </p>
              <p className="text-[11px] text-amber-700 bg-amber-50 border border-amber-200 rounded px-3 py-2">
                This password is shown once. Share it securely with the owner and require a password change on first login.
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

function ProvisioningFlowStepper({ currentStage }: { currentStage: number }) {
  return (
    <div className="space-y-2">
      <div className="grid grid-cols-5 gap-2">
        {FLOW_STAGES.map((label, index) => {
          const isComplete = index < currentStage;
          const isCurrent = index === currentStage;
          return (
            <div key={label} className="space-y-2">
              <div className={`h-1 rounded-full ${isComplete || isCurrent ? 'bg-indigo-600' : 'bg-gray-200'}`} />
              <p className={`text-center text-xs ${isCurrent ? 'text-indigo-700 font-semibold' : 'text-gray-500'}`}>
                {label}
              </p>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function ProvisioningStatusIcon({
  stepNumber,
  isComplete,
  isCurrent,
  isFailed,
}: {
  stepNumber: number;
  isComplete: boolean;
  isCurrent: boolean;
  isFailed: boolean;
}) {
  if (isComplete) {
    return (
      <span className="flex h-7 w-7 items-center justify-center rounded-full bg-green-100 text-green-700">
        <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
        </svg>
      </span>
    );
  }

  if (isFailed) {
    return (
      <span className="flex h-7 w-7 items-center justify-center rounded-full bg-red-100 text-red-700">
        <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </span>
    );
  }

  if (isCurrent) {
    return (
      <span className="flex h-7 w-7 items-center justify-center rounded-full bg-indigo-100 text-indigo-700">
        <span className="h-3.5 w-3.5 rounded-full border-2 border-indigo-300 border-t-indigo-700 animate-spin" />
      </span>
    );
  }

  return (
    <span className="flex h-7 w-7 items-center justify-center rounded-full bg-gray-100 text-gray-400 text-xs font-medium">
      {stepNumber}
    </span>
  );
}

function SummaryItem({
  label,
  value,
  href,
  mono = false,
}: {
  label: string;
  value: string;
  href?: string;
  mono?: boolean;
}) {
  const content = href ? (
    <a href={href} target="_blank" rel="noopener noreferrer" className="text-indigo-700 hover:underline">
      {value}
    </a>
  ) : value;

  return (
    <div className="space-y-1">
      <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500">{label}</p>
      <p className={`${mono ? 'font-mono text-sm' : 'text-sm'} text-gray-900 break-all`}>{content}</p>
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

function buildTenantHostname(slug: string, portalBaseDomain?: string): string {
  const baseDomain = portalBaseDomain?.trim().replace(/^https?:\/\//, '').replace(/\/+$/, '');
  return baseDomain ? `${slug}.${baseDomain}` : slug;
}

function delay(ms: number): Promise<void> {
  return new Promise(resolve => window.setTimeout(resolve, ms));
}
