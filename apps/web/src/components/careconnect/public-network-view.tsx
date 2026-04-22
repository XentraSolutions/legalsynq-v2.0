'use client';

/**
 * CC2-INT-B07 — Public Network View.
 * CC2-INT-B08 — Public Referral Initiation modal.
 *
 * Interactive client component for the public /network page.
 * Shows a searchable provider list with stage badges and "Request Referral" CTAs.
 *
 * Stage enforcement (CC2-INT-B06-02):
 *  - URL           → No portal link. Provider receives referrals via signed token URLs.
 *  - COMMON_PORTAL → "Access Portal" link → redirects to /login (common portal login).
 *  - TENANT        → "Tenant Portal" link → redirects to /login (tenant portal login).
 *
 * Referral flow (CC2-INT-B08):
 *  - "Request Referral" on accepting providers opens a modal form.
 *  - Form submits to POST /api/public/careconnect/api/public/referrals via the BFF.
 *  - The BFF forwards X-Tenant-Id (set here from the server-resolved tenantId prop).
 *  - On success, a confirmation screen is shown.
 */

import { useState, useMemo, useCallback } from 'react';
import type {
  PublicNetworkDetail,
  PublicProviderItem,
  PublicReferralRequest,
  PublicReferralResponse,
} from '@/lib/public-network-api';

interface PublicNetworkViewProps {
  detail:     PublicNetworkDetail;
  tenantCode: string;
  tenantId:   string;
}

export function PublicNetworkView({ detail, tenantCode, tenantId }: PublicNetworkViewProps) {
  const [search, setSearch]         = useState('');
  const [filterActive, setFilter]   = useState<'all' | 'accepting'>('accepting');
  const [modalProvider, setModal]   = useState<PublicProviderItem | null>(null);

  const filtered = useMemo(() => {
    let list = detail.providers;

    if (filterActive === 'accepting') {
      list = list.filter(p => p.acceptingReferrals);
    }

    const q = search.trim().toLowerCase();
    if (q) {
      list = list.filter(p =>
        p.name.toLowerCase().includes(q) ||
        (p.organizationName?.toLowerCase().includes(q) ?? false) ||
        p.city.toLowerCase().includes(q) ||
        p.state.toLowerCase().includes(q),
      );
    }

    return list;
  }, [detail.providers, search, filterActive]);

  return (
    <div className="space-y-4">
      {/* Network header */}
      <div className="bg-white border border-gray-200 rounded-lg p-4">
        <h2 className="text-base font-semibold text-gray-900">{detail.networkName}</h2>
        {detail.networkDescription && (
          <p className="mt-1 text-sm text-gray-500">{detail.networkDescription}</p>
        )}
        <p className="mt-2 text-xs text-gray-400">
          {detail.providers.length} provider{detail.providers.length !== 1 ? 's' : ''} in this network
        </p>
      </div>

      {/* Search + filter bar */}
      <div className="flex flex-col sm:flex-row gap-3">
        <input
          type="search"
          placeholder="Search by name, city, or state…"
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="flex-1 rounded-md border border-gray-200 px-3 py-2 text-sm placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary"
        />
        <div className="flex rounded-md overflow-hidden border border-gray-200 text-sm">
          <button
            onClick={() => setFilter('accepting')}
            className={[
              'px-3 py-2 transition-colors',
              filterActive === 'accepting'
                ? 'bg-primary text-white'
                : 'bg-white text-gray-600 hover:bg-gray-50',
            ].join(' ')}
          >
            Accepting referrals
          </button>
          <button
            onClick={() => setFilter('all')}
            className={[
              'px-3 py-2 border-l border-gray-200 transition-colors',
              filterActive === 'all'
                ? 'bg-primary text-white'
                : 'bg-white text-gray-600 hover:bg-gray-50',
            ].join(' ')}
          >
            All providers
          </button>
        </div>
      </div>

      {/* Provider list */}
      {filtered.length === 0 ? (
        <p className="text-sm text-gray-500 py-8 text-center">
          No providers match your search.
        </p>
      ) : (
        <div className="space-y-3">
          {filtered.map(p => (
            <PublicProviderCard
              key={p.id}
              provider={p}
              onRequestReferral={setModal}
            />
          ))}
        </div>
      )}

      {/* Referral modal */}
      {modalProvider && (
        <ReferralModal
          provider={modalProvider}
          tenantId={tenantId}
          onClose={() => setModal(null)}
        />
      )}
    </div>
  );
}

// ── Provider card ─────────────────────────────────────────────────────────────

function PublicProviderCard({
  provider,
  onRequestReferral,
}: {
  provider: PublicProviderItem;
  onRequestReferral: (p: PublicProviderItem) => void;
}) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg p-4 flex items-start justify-between gap-4">
      {/* Provider details */}
      <div className="min-w-0 flex-1 space-y-1">
        <div className="flex items-center gap-2 flex-wrap">
          <p className="font-medium text-gray-900 truncate">{provider.name}</p>
          <AccessStagePill stage={provider.accessStage} />
          {provider.acceptingReferrals ? (
            <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-green-50 text-green-700 ring-1 ring-inset ring-green-600/20">
              Accepting referrals
            </span>
          ) : (
            <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-gray-100 text-gray-500 ring-1 ring-inset ring-gray-300/40">
              Not accepting
            </span>
          )}
        </div>

        {provider.organizationName && (
          <p className="text-sm text-gray-600 truncate">{provider.organizationName}</p>
        )}

        <p className="text-xs text-gray-500">
          {provider.city}, {provider.state} {provider.postalCode}
        </p>

        {provider.phone && (
          <a
            href={`tel:${provider.phone}`}
            className="text-xs text-primary hover:underline"
          >
            {provider.phone}
          </a>
        )}
      </div>

      {/* Actions */}
      <div className="shrink-0 flex flex-col items-end gap-2">
        <StageAction stage={provider.accessStage} />
        {provider.acceptingReferrals && (
          <button
            onClick={() => onRequestReferral(provider)}
            className="text-xs font-medium px-3 py-1.5 rounded-md bg-primary text-white hover:bg-primary/90 transition-colors"
          >
            Request Referral
          </button>
        )}
      </div>
    </div>
  );
}

// ── Stage badge ───────────────────────────────────────────────────────────────

function AccessStagePill({ stage }: { stage: string }) {
  if (stage === 'COMMON_PORTAL') {
    return (
      <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-blue-50 text-blue-700 ring-1 ring-inset ring-blue-600/20">
        Portal active
      </span>
    );
  }
  if (stage === 'TENANT') {
    return (
      <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-purple-50 text-purple-700 ring-1 ring-inset ring-purple-600/20">
        Tenant portal
      </span>
    );
  }
  return null;
}

// ── Stage-based portal action ─────────────────────────────────────────────────

function StageAction({ stage }: { stage: string }) {
  if (stage === 'COMMON_PORTAL') {
    return (
      <a
        href="/login"
        className="text-xs font-medium text-primary hover:underline"
        title="This provider has activated their portal account"
      >
        View portal
      </a>
    );
  }
  if (stage === 'TENANT') {
    return (
      <a
        href="/login"
        className="text-xs font-medium text-purple-600 hover:underline"
        title="This provider has a dedicated tenant portal"
      >
        Tenant portal
      </a>
    );
  }
  return null;
}

// ── Referral modal (CC2-INT-B08) ──────────────────────────────────────────────

type ModalState = 'form' | 'submitting' | 'success' | 'error';

interface ReferralForm {
  senderName:       string;
  senderEmail:      string;
  patientFirstName: string;
  patientLastName:  string;
  patientPhone:     string;
  patientEmail:     string;
  serviceType:      string;
  notes:            string;
}

const EMPTY_FORM: ReferralForm = {
  senderName:       '',
  senderEmail:      '',
  patientFirstName: '',
  patientLastName:  '',
  patientPhone:     '',
  patientEmail:     '',
  serviceType:      '',
  notes:            '',
};

function ReferralModal({
  provider,
  tenantId,
  onClose,
}: {
  provider: PublicProviderItem;
  tenantId: string;
  onClose:  () => void;
}) {
  const [state,        setState]  = useState<ModalState>('form');
  const [form,         setForm]   = useState<ReferralForm>(EMPTY_FORM);
  const [fieldErrors,  setErrors] = useState<Record<string, string>>({});
  const [errorMessage, setErrMsg] = useState('');
  const [result,       setResult] = useState<PublicReferralResponse | null>(null);

  const update = useCallback(
    (field: keyof ReferralForm, value: string) => {
      setForm(prev => ({ ...prev, [field]: value }));
      setErrors(prev => { const next = { ...prev }; delete next[field]; return next; });
    },
    [],
  );

  const handleSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    setState('submitting');
    setErrors({});
    setErrMsg('');

    const payload: PublicReferralRequest = {
      providerId:       provider.id,
      senderName:       form.senderName,
      senderEmail:      form.senderEmail,
      patientFirstName: form.patientFirstName,
      patientLastName:  form.patientLastName,
      patientPhone:     form.patientPhone,
      patientEmail:     form.patientEmail || undefined,
      serviceType:      form.serviceType   || undefined,
      notes:            form.notes         || undefined,
    };

    try {
      const res = await fetch('/api/public/careconnect/api/public/referrals', {
        method:  'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Tenant-Id':  tenantId,
        },
        body: JSON.stringify(payload),
      });

      if (res.status === 201) {
        const data: PublicReferralResponse = await res.json();
        setResult(data);
        setState('success');
        return;
      }

      const body = await res.json().catch(() => ({}));

      if (res.status === 422 && body.errors) {
        setErrors(body.errors as Record<string, string>);
        setState('form');
        return;
      }

      if (res.status === 429) {
        setErrMsg('Too many submissions. Please wait a minute and try again.');
        setState('error');
        return;
      }

      setErrMsg(body.message ?? 'Something went wrong. Please try again.');
      setState('error');
    } catch {
      setErrMsg('Unable to reach the server. Please check your connection and try again.');
      setState('error');
    }
  }, [form, provider.id, tenantId]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      onClick={e => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-start justify-between p-5 border-b border-gray-100">
          <div>
            <h2 className="text-base font-semibold text-gray-900">Request Referral</h2>
            <p className="mt-0.5 text-sm text-gray-500 truncate">
              {provider.name}{provider.organizationName ? ` · ${provider.organizationName}` : ''}
            </p>
          </div>
          <button
            onClick={onClose}
            className="ml-4 text-gray-400 hover:text-gray-600 transition-colors text-xl leading-none"
            aria-label="Close"
          >
            ×
          </button>
        </div>

        {/* Body */}
        <div className="p-5">
          {state === 'success' && result ? (
            <SuccessScreen result={result} onClose={onClose} />
          ) : state === 'error' ? (
            <ErrorScreen message={errorMessage} onRetry={() => setState('form')} onClose={onClose} />
          ) : (
            <ReferralForm
              form={form}
              fieldErrors={fieldErrors}
              submitting={state === 'submitting'}
              onUpdate={update}
              onSubmit={handleSubmit}
              onCancel={onClose}
            />
          )}
        </div>
      </div>
    </div>
  );
}

// ── Form ──────────────────────────────────────────────────────────────────────

function ReferralForm({
  form,
  fieldErrors,
  submitting,
  onUpdate,
  onSubmit,
  onCancel,
}: {
  form:        ReferralForm;
  fieldErrors: Record<string, string>;
  submitting:  boolean;
  onUpdate:    (field: keyof ReferralForm, value: string) => void;
  onSubmit:    (e: React.FormEvent) => void;
  onCancel:    () => void;
}) {
  return (
    <form onSubmit={onSubmit} className="space-y-5">
      {/* Sender info */}
      <fieldset className="space-y-3">
        <legend className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
          Your information
        </legend>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field
            label="Your name"
            required
            error={fieldErrors['senderName']}
          >
            <input
              type="text"
              required
              autoComplete="name"
              value={form.senderName}
              onChange={e => onUpdate('senderName', e.target.value)}
              disabled={submitting}
              className={inputClass(!!fieldErrors['senderName'])}
              placeholder="Jane Smith"
            />
          </Field>
          <Field
            label="Your email"
            required
            error={fieldErrors['senderEmail']}
          >
            <input
              type="email"
              required
              autoComplete="email"
              value={form.senderEmail}
              onChange={e => onUpdate('senderEmail', e.target.value)}
              disabled={submitting}
              className={inputClass(!!fieldErrors['senderEmail'])}
              placeholder="jane@lawfirm.com"
            />
          </Field>
        </div>
      </fieldset>

      {/* Patient info */}
      <fieldset className="space-y-3">
        <legend className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
          Patient information
        </legend>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field
            label="First name"
            required
            error={fieldErrors['patientFirstName']}
          >
            <input
              type="text"
              required
              value={form.patientFirstName}
              onChange={e => onUpdate('patientFirstName', e.target.value)}
              disabled={submitting}
              className={inputClass(!!fieldErrors['patientFirstName'])}
              placeholder="John"
            />
          </Field>
          <Field
            label="Last name"
            required
            error={fieldErrors['patientLastName']}
          >
            <input
              type="text"
              required
              value={form.patientLastName}
              onChange={e => onUpdate('patientLastName', e.target.value)}
              disabled={submitting}
              className={inputClass(!!fieldErrors['patientLastName'])}
              placeholder="Doe"
            />
          </Field>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field
            label="Phone"
            required
            error={fieldErrors['patientPhone']}
          >
            <input
              type="tel"
              required
              autoComplete="tel"
              value={form.patientPhone}
              onChange={e => onUpdate('patientPhone', e.target.value)}
              disabled={submitting}
              className={inputClass(!!fieldErrors['patientPhone'])}
              placeholder="(555) 000-0000"
            />
          </Field>
          <Field
            label="Email"
            hint="Optional"
            error={fieldErrors['patientEmail']}
          >
            <input
              type="email"
              autoComplete="email"
              value={form.patientEmail}
              onChange={e => onUpdate('patientEmail', e.target.value)}
              disabled={submitting}
              className={inputClass(!!fieldErrors['patientEmail'])}
              placeholder="patient@example.com"
            />
          </Field>
        </div>
      </fieldset>

      {/* Optional details */}
      <fieldset className="space-y-3">
        <legend className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
          Referral details
        </legend>
        <Field
          label="Service type"
          hint="Optional"
          error={fieldErrors['serviceType']}
        >
          <input
            type="text"
            value={form.serviceType}
            onChange={e => onUpdate('serviceType', e.target.value)}
            disabled={submitting}
            className={inputClass(!!fieldErrors['serviceType'])}
            placeholder="e.g. Physical therapy, Occupational therapy"
          />
        </Field>
        <Field
          label="Case notes"
          hint="Optional"
          error={fieldErrors['notes']}
        >
          <textarea
            rows={3}
            value={form.notes}
            onChange={e => onUpdate('notes', e.target.value)}
            disabled={submitting}
            className={inputClass(!!fieldErrors['notes'])}
            placeholder="Any relevant background or context for the provider…"
          />
        </Field>
      </fieldset>

      {/* Footer */}
      <div className="flex justify-end gap-3 pt-2">
        <button
          type="button"
          onClick={onCancel}
          disabled={submitting}
          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-200 rounded-md hover:bg-gray-50 transition-colors disabled:opacity-50"
        >
          Cancel
        </button>
        <button
          type="submit"
          disabled={submitting}
          className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-md hover:bg-primary/90 transition-colors disabled:opacity-60 flex items-center gap-2"
        >
          {submitting ? (
            <>
              <span className="inline-block w-3 h-3 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              Sending…
            </>
          ) : (
            'Send Referral'
          )}
        </button>
      </div>
    </form>
  );
}

// ── Success screen ────────────────────────────────────────────────────────────

function SuccessScreen({
  result,
  onClose,
}: {
  result:  PublicReferralResponse;
  onClose: () => void;
}) {
  return (
    <div className="text-center py-4 space-y-4">
      <div className="mx-auto w-12 h-12 rounded-full bg-green-50 flex items-center justify-center text-2xl">
        ✓
      </div>
      <div>
        <h3 className="text-base font-semibold text-gray-900">Referral sent successfully</h3>
        <p className="mt-1 text-sm text-gray-500">{result.message}</p>
        <p className="mt-1 text-xs text-gray-400">
          Provider: {result.providerName}
        </p>
      </div>
      {result.providerStage === 'URL' && (
        <p className="text-xs text-gray-500 bg-blue-50 rounded-md p-3">
          This provider will receive a secure link to view and respond to your referral by email.
        </p>
      )}
      {(result.providerStage === 'COMMON_PORTAL' || result.providerStage === 'TENANT') && (
        <p className="text-xs text-gray-500 bg-blue-50 rounded-md p-3">
          This provider has an active portal and will be notified immediately.
        </p>
      )}
      <button
        onClick={onClose}
        className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-md hover:bg-primary/90 transition-colors"
      >
        Close
      </button>
    </div>
  );
}

// ── Error screen ──────────────────────────────────────────────────────────────

function ErrorScreen({
  message,
  onRetry,
  onClose,
}: {
  message:  string;
  onRetry:  () => void;
  onClose:  () => void;
}) {
  return (
    <div className="text-center py-4 space-y-4">
      <div className="mx-auto w-12 h-12 rounded-full bg-red-50 flex items-center justify-center text-2xl">
        !
      </div>
      <div>
        <h3 className="text-base font-semibold text-gray-900">Submission failed</h3>
        <p className="mt-1 text-sm text-gray-500">{message}</p>
      </div>
      <div className="flex justify-center gap-3">
        <button
          onClick={onClose}
          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-200 rounded-md hover:bg-gray-50 transition-colors"
        >
          Close
        </button>
        <button
          onClick={onRetry}
          className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-md hover:bg-primary/90 transition-colors"
        >
          Try again
        </button>
      </div>
    </div>
  );
}

// ── Field wrapper ─────────────────────────────────────────────────────────────

function Field({
  label,
  hint,
  required,
  error,
  children,
}: {
  label:    string;
  hint?:    string;
  required?: boolean;
  error?:   string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1">
      <label className="flex items-center gap-1 text-xs font-medium text-gray-700">
        {label}
        {required && <span className="text-red-500">*</span>}
        {hint && <span className="text-gray-400 font-normal">({hint})</span>}
      </label>
      {children}
      {error && <p className="text-xs text-red-600">{error}</p>}
    </div>
  );
}

function inputClass(hasError: boolean) {
  return [
    'w-full rounded-md border px-3 py-2 text-sm text-gray-900 placeholder-gray-400',
    'focus:outline-none focus:ring-2 transition-colors',
    'disabled:bg-gray-50 disabled:text-gray-500',
    hasError
      ? 'border-red-300 focus:ring-red-200 focus:border-red-400'
      : 'border-gray-200 focus:ring-primary/30 focus:border-primary',
  ].join(' ');
}
