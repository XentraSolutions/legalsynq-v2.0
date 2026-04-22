'use client';

/**
 * CC2-INT-B07 — Public Network View (Yelp-style).
 * CC2-INT-B08 — Public Referral Initiation modal.
 *
 * Split-panel layout: sticky search header, scrollable numbered list on the
 * left, sticky live map on the right (hidden on mobile).
 *
 * Stage enforcement (CC2-INT-B06-02):
 *  - URL           → No portal link.
 *  - COMMON_PORTAL → "Access Portal" link → /login.
 *  - TENANT        → "Tenant Portal" link → /login.
 *
 * Referral flow (CC2-INT-B08):
 *  - "Request Referral" opens a modal; submits to the BFF which forwards
 *    X-Tenant-Id to the AllowAnonymous CareConnect public referral endpoint.
 */

import { useState, useMemo, useCallback, useRef, forwardRef, useEffect, type FormEvent, type ReactNode } from 'react';
import dynamic from 'next/dynamic';
import type {
  PublicNetworkDetail,
  PublicProviderItem,
  PublicProviderMarker,
  PublicReferralRequest,
  PublicReferralResponse,
} from '@/lib/public-network-api';
import type { NumberedMarker } from './public-network-map';

const PublicNetworkMap = dynamic(
  () => import('./public-network-map').then(m => m.PublicNetworkMap),
  { ssr: false, loading: () => <div className="h-full w-full bg-gray-100 animate-pulse" /> },
);

interface PublicNetworkViewProps {
  detail:     PublicNetworkDetail;
  tenantCode: string;
  tenantId:   string;
}

type Filter = 'accepting' | 'all';

// ── Main view ─────────────────────────────────────────────────────────────────

export function PublicNetworkView({ detail, tenantCode, tenantId }: PublicNetworkViewProps) {
  const [search,    setSearch]  = useState('');
  const [filter,    setFilter]  = useState<Filter>('accepting');
  const [modal,     setModal]   = useState<PublicProviderItem | null>(null);
  const [hoveredId, setHovered] = useState<string | null>(null);
  const cardRefs = useRef<Record<string, HTMLDivElement | null>>({});

  // Markers: start from what the backend returned; auto-geocode missing ones
  const [markers, setMarkers] = useState<PublicProviderMarker[]>(detail.markers);

  useEffect(() => {
    // Nothing to geocode if the backend already gave us coordinates
    if (detail.providers.length === 0) return;
    const missing = detail.providers.filter(
      p => !detail.markers.some(m => m.id === p.id),
    );
    if (missing.length === 0) return;

    let cancelled = false;

    async function geocodeMissing() {
      const results: PublicProviderMarker[] = [...detail.markers];

      await Promise.all(
        missing.map(async p => {
          const q = [p.city, p.state, p.postalCode].filter(Boolean).join(' ');
          if (!q) return;
          try {
            const res = await fetch(
              `/api/geocode/address?q=${encodeURIComponent(q)}`,
            );
            if (!res.ok) return;
            const suggestions = await res.json() as Array<{
              latitude: number; longitude: number;
            }>;
            if (suggestions.length === 0) return;
            const { latitude, longitude } = suggestions[0];
            results.push({
              id:               p.id,
              name:             p.name,
              organizationName: p.organizationName,
              city:             p.city,
              state:            p.state,
              acceptingReferrals: p.acceptingReferrals,
              latitude,
              longitude,
            });
          } catch { /* ignore */ }
        }),
      );

      if (!cancelled) setMarkers(results);
    }

    geocodeMissing();
    return () => { cancelled = true; };
  // Only run once on mount — detail is server-side stable
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Build a lookup of marker data by provider id
  const markerById = useMemo<Record<string, PublicProviderMarker>>(() => {
    const m: Record<string, PublicProviderMarker> = {};
    for (const mk of markers) m[mk.id] = mk;
    return m;
  }, [markers]);

  // Filtered + searched provider list
  const filtered = useMemo(() => {
    let list = detail.providers;
    if (filter === 'accepting') list = list.filter(p => p.acceptingReferrals);
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
  }, [detail.providers, search, filter]);

  // Numbered map markers — only providers that have geo data, in filtered order
  const displayedMarkers = useMemo<NumberedMarker[]>(() => {
    const result: NumberedMarker[] = [];
    let idx = 1;
    for (const p of filtered) {
      const mk = markerById[p.id];
      if (mk) result.push({ ...mk, index: idx++ });
    }
    return result;
  }, [filtered, markerById]);

  // Map marker index for a given provider id
  const indexFor = (id: string) =>
    displayedMarkers.find(m => m.id === id)?.index ?? null;

  function handleMapReferral(m: PublicProviderMarker) {
    const provider = detail.providers.find(p => p.id === m.id) ?? null;
    if (provider) setModal(provider);
  }

  function handleMapSelect(id: string) {
    setHovered(id);
    // Scroll the matching card into view
    cardRefs.current[id]?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  const hasMarkers = markers.length > 0;

  return (
    <div className="flex flex-col min-h-screen bg-gray-50">

      {/* ── Sticky header ────────────────────────────────────────────────────── */}
      <header className="sticky top-0 z-40 bg-white border-b border-gray-200 shadow-sm">
        <div className="max-w-[1600px] mx-auto px-4 py-3 flex items-center gap-4">
          {/* Brand + network name */}
          <div className="flex items-center gap-2.5 flex-shrink-0">
            <div className="w-7 h-7 rounded-md bg-red-600 flex items-center justify-center">
              <i className="ri-heart-pulse-line text-white text-sm" />
            </div>
            <span className="font-bold text-gray-900 text-base hidden sm:block truncate max-w-[200px]">
              {detail.networkName}
            </span>
          </div>

          {/* Search bar */}
          <div className="flex-1 relative">
            <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm pointer-events-none" />
            <input
              type="search"
              placeholder="Search providers, specialties, cities…"
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="w-full pl-9 pr-4 py-2 text-sm bg-gray-100 border border-gray-200 rounded-full
                         focus:outline-none focus:bg-white focus:border-red-400 focus:ring-2 focus:ring-red-100
                         transition-colors"
            />
          </div>

          {/* Provider count */}
          <span className="text-xs text-gray-400 flex-shrink-0 hidden md:block">
            {filtered.length} result{filtered.length !== 1 ? 's' : ''}
          </span>
        </div>

        {/* Filter chips */}
        <div className="max-w-[1600px] mx-auto px-4 pb-2 flex items-center gap-2">
          <FilterChip
            label="Accepting referrals"
            icon="ri-checkbox-circle-line"
            active={filter === 'accepting'}
            onClick={() => setFilter('accepting')}
          />
          <FilterChip
            label="All providers"
            icon="ri-team-line"
            active={filter === 'all'}
            onClick={() => setFilter('all')}
          />
          {search && (
            <button
              onClick={() => setSearch('')}
              className="ml-auto text-xs text-gray-400 hover:text-gray-600 flex items-center gap-1 transition-colors"
            >
              <i className="ri-close-line" />
              Clear search
            </button>
          )}
        </div>
      </header>

      {/* ── Split body ───────────────────────────────────────────────────────── */}
      <div
        className="flex flex-1 overflow-hidden"
        style={{ height: 'calc(100vh - 97px)' }}
      >
        {/* Left: scrollable provider list */}
        <div className="w-full lg:w-[460px] xl:w-[520px] flex-shrink-0 overflow-y-auto">
          {/* Result summary strip */}
          <div className="px-4 pt-3 pb-1">
            <p className="text-xs font-medium text-gray-400 uppercase tracking-wide">
              {filtered.length === 0
                ? 'No providers found'
                : `${filtered.length} provider${filtered.length !== 1 ? 's' : ''} found`}
            </p>
          </div>

          {filtered.length === 0 ? (
            <div className="px-4 py-12 text-center">
              <div className="mx-auto w-12 h-12 rounded-full bg-gray-100 flex items-center justify-center mb-3">
                <i className="ri-map-pin-line text-gray-400 text-xl" />
              </div>
              <p className="text-sm font-medium text-gray-600">No providers match your search</p>
              <p className="text-xs text-gray-400 mt-1">Try adjusting your filters or search terms</p>
            </div>
          ) : (
            <div className="p-3 space-y-2">
              {filtered.map((provider, i) => (
                <ProviderCard
                  key={provider.id}
                  provider={provider}
                  number={indexFor(provider.id) ?? i + 1}
                  highlighted={hoveredId === provider.id}
                  onHover={setHovered}
                  onRequestReferral={setModal}
                  ref={el => { cardRefs.current[provider.id] = el; }}
                />
              ))}
            </div>
          )}
        </div>

        {/* Right: sticky map */}
        <div className="hidden lg:block flex-1 relative">
          {hasMarkers ? (
            <PublicNetworkMap
              markers={displayedMarkers}
              selectedId={hoveredId}
              onSelect={handleMapSelect}
              onRequestReferral={handleMapReferral}
            />
          ) : (
            <div className="h-full bg-gray-100 flex items-center justify-center">
              <p className="text-sm text-gray-400">No location data available</p>
            </div>
          )}
        </div>
      </div>

      {/* ── Referral modal ───────────────────────────────────────────────────── */}
      {modal && (
        <ReferralModal
          provider={modal}
          tenantId={tenantId}
          onClose={() => setModal(null)}
        />
      )}
    </div>
  );
}

// ── Filter chip ───────────────────────────────────────────────────────────────

function FilterChip({
  label, icon, active, onClick,
}: {
  label: string; icon: string; active: boolean; onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className={[
        'flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium border transition-colors',
        active
          ? 'bg-red-600 border-red-600 text-white'
          : 'bg-white border-gray-200 text-gray-600 hover:border-red-300 hover:text-red-600',
      ].join(' ')}
    >
      <i className={`${icon} text-xs`} />
      {label}
    </button>
  );
}

// ── Provider card ─────────────────────────────────────────────────────────────

const ProviderCard = forwardRef<
  HTMLDivElement,
  {
    provider:          PublicProviderItem;
    number:            number;
    highlighted:       boolean;
    onHover:           (id: string | null) => void;
    onRequestReferral: (p: PublicProviderItem) => void;
  }
>(function ProviderCard({ provider, number, highlighted, onHover, onRequestReferral }, ref) {
  return (
    <div
      ref={ref}
      onMouseEnter={() => onHover(provider.id)}
      onMouseLeave={() => onHover(null)}
      className={[
        'bg-white rounded-xl border p-4 cursor-default transition-all duration-150',
        highlighted
          ? 'border-red-400 shadow-md ring-1 ring-red-200'
          : 'border-gray-200 shadow-sm hover:border-gray-300 hover:shadow',
      ].join(' ')}
    >
      <div className="flex gap-3">
        {/* Number badge */}
        <div
          className={[
            'flex-shrink-0 w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold text-white mt-0.5',
            provider.acceptingReferrals ? 'bg-red-600' : 'bg-gray-400',
          ].join(' ')}
        >
          {number}
        </div>

        {/* Content */}
        <div className="flex-1 min-w-0">
          {/* Name row */}
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0">
              <h3 className="font-bold text-gray-900 text-sm leading-snug truncate">
                {provider.name}
              </h3>
              {provider.organizationName && (
                <p className="text-xs text-gray-500 truncate">{provider.organizationName}</p>
              )}
            </div>

            {/* Stage badge */}
            <StagePill stage={provider.accessStage} />
          </div>

          {/* Status pills */}
          <div className="flex flex-wrap items-center gap-1.5 mt-2">
            {provider.acceptingReferrals ? (
              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-green-50 text-green-700 ring-1 ring-inset ring-green-600/20">
                <i className="ri-checkbox-circle-fill text-xs" />
                Accepting referrals
              </span>
            ) : (
              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-500 ring-1 ring-inset ring-gray-300/40">
                <i className="ri-close-circle-line text-xs" />
                Not accepting
              </span>
            )}
            {provider.primaryCategory && (
              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-700 ring-1 ring-inset ring-blue-600/20">
                {provider.primaryCategory}
              </span>
            )}
          </div>

          {/* Location + phone */}
          <div className="mt-2 space-y-0.5">
            <p className="flex items-center gap-1.5 text-xs text-gray-500">
              <i className="ri-map-pin-line text-gray-400 flex-shrink-0" />
              {provider.city}, {provider.state} {provider.postalCode}
            </p>
            {provider.phone && (
              <a
                href={`tel:${provider.phone}`}
                className="flex items-center gap-1.5 text-xs text-red-600 hover:underline w-fit"
              >
                <i className="ri-phone-line flex-shrink-0" />
                {provider.phone}
              </a>
            )}
          </div>

          {/* Actions */}
          <div className="mt-3 flex items-center gap-2">
            {provider.acceptingReferrals && (
              <button
                onClick={() => onRequestReferral(provider)}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-red-600 text-white text-xs font-semibold hover:bg-red-700 transition-colors"
              >
                <i className="ri-send-plane-line" />
                Request Referral
              </button>
            )}
            <StageAction stage={provider.accessStage} />
          </div>
        </div>
      </div>
    </div>
  );
});

// ── Stage pill ────────────────────────────────────────────────────────────────

function StagePill({ stage }: { stage: string }) {
  if (stage === 'COMMON_PORTAL') {
    return (
      <span className="flex-shrink-0 inline-flex items-center rounded-full px-1.5 py-0.5 text-xs font-medium bg-blue-50 text-blue-700 ring-1 ring-inset ring-blue-600/20 whitespace-nowrap">
        Portal active
      </span>
    );
  }
  if (stage === 'TENANT') {
    return (
      <span className="flex-shrink-0 inline-flex items-center rounded-full px-1.5 py-0.5 text-xs font-medium bg-purple-50 text-purple-700 ring-1 ring-inset ring-purple-600/20 whitespace-nowrap">
        Tenant portal
      </span>
    );
  }
  return null;
}

// ── Stage action ──────────────────────────────────────────────────────────────

function StageAction({ stage }: { stage: string }) {
  if (stage === 'COMMON_PORTAL') {
    return (
      <a
        href="/login"
        className="flex items-center gap-1 text-xs text-blue-600 font-medium hover:underline"
      >
        <i className="ri-external-link-line" />
        View portal
      </a>
    );
  }
  if (stage === 'TENANT') {
    return (
      <a
        href="/login"
        className="flex items-center gap-1 text-xs text-purple-600 font-medium hover:underline"
      >
        <i className="ri-external-link-line" />
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

  const handleSubmit = useCallback(async (e: FormEvent) => {
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
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4"
      onClick={e => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-start justify-between p-5 border-b border-gray-100">
          <div className="flex items-start gap-3">
            <div className="w-8 h-8 rounded-full bg-red-100 flex items-center justify-center flex-shrink-0 mt-0.5">
              <i className="ri-send-plane-line text-red-600 text-sm" />
            </div>
            <div>
              <h2 className="text-base font-bold text-gray-900">Request Referral</h2>
              <p className="mt-0.5 text-sm text-gray-500 truncate max-w-[320px]">
                {provider.name}{provider.organizationName ? ` · ${provider.organizationName}` : ''}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="ml-4 w-7 h-7 rounded-full bg-gray-100 hover:bg-gray-200 flex items-center justify-center text-gray-500 transition-colors"
            aria-label="Close"
          >
            <i className="ri-close-line text-sm" />
          </button>
        </div>

        {/* Body */}
        <div className="p-5">
          {state === 'success' && result ? (
            <SuccessScreen result={result} onClose={onClose} />
          ) : state === 'error' ? (
            <ErrorScreen message={errorMessage} onRetry={() => setState('form')} onClose={onClose} />
          ) : (
            <ReferralFormBody
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

function ReferralFormBody({
  form, fieldErrors, submitting, onUpdate, onSubmit, onCancel,
}: {
  form:        ReferralForm;
  fieldErrors: Record<string, string>;
  submitting:  boolean;
  onUpdate:    (field: keyof ReferralForm, value: string) => void;
  onSubmit:    (e: FormEvent) => void;
  onCancel:    () => void;
}) {
  return (
    <form onSubmit={onSubmit} className="space-y-5">
      <fieldset className="space-y-3">
        <legend className="text-xs font-semibold text-gray-400 uppercase tracking-wide">Your information</legend>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label="Your name" required error={fieldErrors['senderName']}>
            <input type="text" required autoComplete="name" value={form.senderName}
              onChange={e => onUpdate('senderName', e.target.value)} disabled={submitting}
              className={inputCls(!!fieldErrors['senderName'])} placeholder="Jane Smith" />
          </Field>
          <Field label="Your email" required error={fieldErrors['senderEmail']}>
            <input type="email" required autoComplete="email" value={form.senderEmail}
              onChange={e => onUpdate('senderEmail', e.target.value)} disabled={submitting}
              className={inputCls(!!fieldErrors['senderEmail'])} placeholder="jane@lawfirm.com" />
          </Field>
        </div>
      </fieldset>

      <fieldset className="space-y-3">
        <legend className="text-xs font-semibold text-gray-400 uppercase tracking-wide">Patient information</legend>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label="First name" required error={fieldErrors['patientFirstName']}>
            <input type="text" required value={form.patientFirstName}
              onChange={e => onUpdate('patientFirstName', e.target.value)} disabled={submitting}
              className={inputCls(!!fieldErrors['patientFirstName'])} placeholder="John" />
          </Field>
          <Field label="Last name" required error={fieldErrors['patientLastName']}>
            <input type="text" required value={form.patientLastName}
              onChange={e => onUpdate('patientLastName', e.target.value)} disabled={submitting}
              className={inputCls(!!fieldErrors['patientLastName'])} placeholder="Doe" />
          </Field>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label="Phone" required error={fieldErrors['patientPhone']}>
            <input type="tel" required autoComplete="tel" value={form.patientPhone}
              onChange={e => onUpdate('patientPhone', e.target.value)} disabled={submitting}
              className={inputCls(!!fieldErrors['patientPhone'])} placeholder="(555) 000-0000" />
          </Field>
          <Field label="Email" hint="Optional" error={fieldErrors['patientEmail']}>
            <input type="email" autoComplete="email" value={form.patientEmail}
              onChange={e => onUpdate('patientEmail', e.target.value)} disabled={submitting}
              className={inputCls(!!fieldErrors['patientEmail'])} placeholder="patient@example.com" />
          </Field>
        </div>
      </fieldset>

      <fieldset className="space-y-3">
        <legend className="text-xs font-semibold text-gray-400 uppercase tracking-wide">Referral details</legend>
        <Field label="Service type" hint="Optional" error={fieldErrors['serviceType']}>
          <input type="text" value={form.serviceType}
            onChange={e => onUpdate('serviceType', e.target.value)} disabled={submitting}
            className={inputCls(!!fieldErrors['serviceType'])}
            placeholder="e.g. Physical therapy, Occupational therapy" />
        </Field>
        <Field label="Case notes" hint="Optional" error={fieldErrors['notes']}>
          <textarea rows={3} value={form.notes}
            onChange={e => onUpdate('notes', e.target.value)} disabled={submitting}
            className={inputCls(!!fieldErrors['notes'])}
            placeholder="Any relevant background or context for the provider…" />
        </Field>
      </fieldset>

      <div className="flex justify-end gap-3 pt-1">
        <button type="button" onClick={onCancel} disabled={submitting}
          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors disabled:opacity-50">
          Cancel
        </button>
        <button type="submit" disabled={submitting}
          className="px-5 py-2 text-sm font-semibold text-white bg-red-600 rounded-lg hover:bg-red-700 transition-colors disabled:opacity-60 flex items-center gap-2">
          {submitting ? (
            <>
              <span className="w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              Sending…
            </>
          ) : (
            <>
              <i className="ri-send-plane-line" />
              Send Referral
            </>
          )}
        </button>
      </div>
    </form>
  );
}

// ── Shared form helpers ───────────────────────────────────────────────────────

function Field({
  label, hint, required, error, children,
}: {
  label: string; hint?: string; required?: boolean; error?: string; children: ReactNode;
}) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-600 mb-1">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
        {hint && <span className="ml-1 text-gray-400 font-normal">({hint})</span>}
      </label>
      {children}
      {error && <p className="mt-1 text-xs text-red-600">{error}</p>}
    </div>
  );
}

function inputCls(hasError: boolean) {
  return [
    'w-full rounded-lg border px-3 py-2 text-sm focus:outline-none focus:ring-2 transition-colors',
    hasError
      ? 'border-red-300 focus:border-red-400 focus:ring-red-100'
      : 'border-gray-200 focus:border-red-400 focus:ring-red-100',
  ].join(' ');
}

// ── Success screen ────────────────────────────────────────────────────────────

function SuccessScreen({ result, onClose }: { result: PublicReferralResponse; onClose: () => void }) {
  return (
    <div className="text-center py-6 space-y-4">
      <div className="mx-auto w-14 h-14 rounded-full bg-green-50 flex items-center justify-center">
        <i className="ri-checkbox-circle-line text-green-600 text-3xl" />
      </div>
      <div>
        <h3 className="text-base font-bold text-gray-900">Referral sent!</h3>
        <p className="mt-1 text-sm text-gray-500">{result.message}</p>
        <p className="mt-0.5 text-xs text-gray-400">Provider: {result.providerName}</p>
      </div>
      {result.providerStage === 'URL' && (
        <p className="text-xs text-gray-500 bg-blue-50 rounded-lg p-3">
          This provider will receive a secure link to view and respond to your referral by email.
        </p>
      )}
      {(result.providerStage === 'COMMON_PORTAL' || result.providerStage === 'TENANT') && (
        <p className="text-xs text-gray-500 bg-blue-50 rounded-lg p-3">
          This provider has an active portal and will be notified immediately.
        </p>
      )}
      <button
        onClick={onClose}
        className="px-5 py-2 text-sm font-semibold text-white bg-red-600 rounded-lg hover:bg-red-700 transition-colors"
      >
        Done
      </button>
    </div>
  );
}

// ── Error screen ──────────────────────────────────────────────────────────────

function ErrorScreen({ message, onRetry, onClose }: { message: string; onRetry: () => void; onClose: () => void }) {
  return (
    <div className="text-center py-6 space-y-4">
      <div className="mx-auto w-14 h-14 rounded-full bg-red-50 flex items-center justify-center">
        <i className="ri-error-warning-line text-red-500 text-3xl" />
      </div>
      <div>
        <h3 className="text-base font-bold text-gray-900">Submission failed</h3>
        <p className="mt-1 text-sm text-gray-500">{message}</p>
      </div>
      <div className="flex justify-center gap-3">
        <button onClick={onClose}
          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">
          Close
        </button>
        <button onClick={onRetry}
          className="px-4 py-2 text-sm font-semibold text-white bg-red-600 rounded-lg hover:bg-red-700 transition-colors">
          Try again
        </button>
      </div>
    </div>
  );
}
