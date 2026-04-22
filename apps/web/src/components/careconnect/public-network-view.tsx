'use client';

/**
 * CC2-INT-B07 — Public Network View.
 * CC2-INT-B08 — Public Referral Initiation.
 *
 * Three-panel layout (Split mode): compact provider list | live map | referral panel.
 * View modes: Split (default) | List | Map.
 * Multi-select providers → persistent side panel with Patient / Law Firm / Providers sections.
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

type ViewMode = 'split' | 'list' | 'map';

// ── Main view ─────────────────────────────────────────────────────────────────

export function PublicNetworkView({ detail, tenantCode, tenantId }: PublicNetworkViewProps) {
  const [search,      setSearch]      = useState('');
  const [viewMode,    setViewMode]    = useState<ViewMode>('split');
  const [showAll,     setShowAll]     = useState(false);   // false = accepting only
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [hoveredId,   setHovered]     = useState<string | null>(null);
  const [panelOpen,   setPanelOpen]   = useState(false);
  const cardRefs = useRef<Record<string, HTMLDivElement | null>>({});

  // Markers: start from backend data; auto-geocode providers missing coordinates
  const [markers, setMarkers] = useState<PublicProviderMarker[]>(detail.markers);

  useEffect(() => {
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
            const res = await fetch(`/api/geocode/address?q=${encodeURIComponent(q)}`);
            if (!res.ok) return;
            const suggestions = await res.json() as Array<{ latitude: number; longitude: number }>;
            if (suggestions.length === 0) return;
            const { latitude, longitude } = suggestions[0];
            results.push({
              id: p.id, name: p.name, organizationName: p.organizationName,
              city: p.city, state: p.state, acceptingReferrals: p.acceptingReferrals,
              latitude, longitude,
            });
          } catch { /* ignore */ }
        }),
      );
      if (!cancelled) setMarkers(results);
    }

    geocodeMissing();
    return () => { cancelled = true; };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Marker lookup by provider id
  const markerById = useMemo<Record<string, PublicProviderMarker>>(() => {
    const m: Record<string, PublicProviderMarker> = {};
    for (const mk of markers) m[mk.id] = mk;
    return m;
  }, [markers]);

  // Filtered + searched list
  const filtered = useMemo(() => {
    let list = detail.providers;
    if (!showAll) list = list.filter(p => p.acceptingReferrals);
    const q = search.trim().toLowerCase();
    if (q) list = list.filter(p =>
      p.name.toLowerCase().includes(q) ||
      (p.organizationName?.toLowerCase().includes(q) ?? false) ||
      p.city.toLowerCase().includes(q) ||
      p.state.toLowerCase().includes(q),
    );
    return list;
  }, [detail.providers, search, showAll]);

  // Numbered markers for the map
  const displayedMarkers = useMemo<NumberedMarker[]>(() => {
    const result: NumberedMarker[] = [];
    let idx = 1;
    for (const p of filtered) {
      const mk = markerById[p.id];
      if (mk) result.push({ ...mk, index: idx++ });
    }
    return result;
  }, [filtered, markerById]);

  const indexFor = (id: string) =>
    displayedMarkers.find(m => m.id === id)?.index ?? null;

  function toggleSelect(id: string) {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
    setPanelOpen(true);
  }

  function handleMapSelect(id: string) {
    setHovered(id);
    cardRefs.current[id]?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  function handleMapReferral(m: PublicProviderMarker) {
    toggleSelect(m.id);
  }

  const selectedProviders = detail.providers.filter(p => selectedIds.has(p.id));
  const hasMarkers        = markers.length > 0;
  const allCount          = detail.providers.filter(p => p.acceptingReferrals).length;
  const shownCount        = filtered.length;

  const showList = viewMode === 'split' || viewMode === 'list';
  const showMap  = viewMode === 'split' || viewMode === 'map';

  return (
    <div className="flex flex-col h-screen bg-white overflow-hidden">

      {/* ── Header ─────────────────────────────────────────────────────────── */}
      <header className="flex-shrink-0 border-b border-gray-200 bg-white">
        {/* Row 1: network name + tenant + count */}
        <div className="flex items-center gap-3 px-4 pt-3 pb-2">
          <h1 className="text-lg font-bold text-gray-900 leading-tight">
            {detail.networkName}
          </h1>
          <span className="text-xs font-semibold text-gray-500 tracking-widest uppercase bg-gray-100 px-2 py-0.5 rounded">
            {tenantCode.replace(/-/g, ' ')}
          </span>
          <span className="ml-auto text-sm text-gray-500">
            {detail.providers.length} provider{detail.providers.length !== 1 ? 's' : ''}
          </span>
        </div>

        {/* Row 2: view tabs + search + filters */}
        <div className="flex items-center gap-2 px-4 pb-2">
          {/* View tabs */}
          <div className="flex items-center border border-gray-200 rounded-md overflow-hidden flex-shrink-0">
            {(['split', 'list', 'map'] as ViewMode[]).map(m => (
              <button
                key={m}
                onClick={() => setViewMode(m)}
                className={[
                  'px-3 py-1.5 text-xs font-medium capitalize transition-colors',
                  viewMode === m
                    ? 'bg-gray-900 text-white'
                    : 'bg-white text-gray-600 hover:bg-gray-50',
                ].join(' ')}
              >
                {m}
              </button>
            ))}
          </div>

          {/* Search */}
          <div className="flex-1 relative">
            <i className="ri-search-line absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 text-sm pointer-events-none" />
            <input
              type="search"
              placeholder="Search providers..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="w-full pl-8 pr-3 py-1.5 text-sm border border-gray-200 rounded-md
                         focus:outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-100
                         placeholder-gray-400"
            />
          </div>

          {/* Filter button */}
          <button
            onClick={() => setShowAll(v => !v)}
            className={[
              'flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium border rounded-md flex-shrink-0 transition-colors',
              showAll
                ? 'bg-gray-900 text-white border-gray-900'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-300',
            ].join(' ')}
          >
            <i className="ri-filter-3-line" />
            Filters
          </button>

          {/* Filter count */}
          <span className="text-xs text-gray-500 font-medium flex-shrink-0">
            {shownCount}/{detail.providers.length}
          </span>

          {/* Selected badge */}
          {selectedIds.size > 0 && (
            <button
              onClick={() => setPanelOpen(v => !v)}
              className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold bg-blue-600 text-white rounded-md flex-shrink-0 hover:bg-blue-700 transition-colors"
            >
              {selectedIds.size} Selected
            </button>
          )}
        </div>
      </header>

      {/* ── Body ───────────────────────────────────────────────────────────── */}
      <div className="flex flex-1 overflow-hidden">

        {/* Left: provider list */}
        {showList && (
          <div className="w-[220px] flex-shrink-0 border-r border-gray-200 overflow-y-auto bg-white">
            {filtered.length === 0 ? (
              <div className="p-4 text-center">
                <p className="text-xs text-gray-400">No providers found</p>
              </div>
            ) : (
              <div className="py-1">
                {filtered.map((provider, i) => (
                  <ProviderRow
                    key={provider.id}
                    provider={provider}
                    number={indexFor(provider.id) ?? i + 1}
                    selected={selectedIds.has(provider.id)}
                    hovered={hoveredId === provider.id}
                    onHover={setHovered}
                    onToggle={toggleSelect}
                    ref={el => { cardRefs.current[provider.id] = el; }}
                  />
                ))}
              </div>
            )}
          </div>
        )}

        {/* Center: map */}
        {showMap && (
          <div className="flex-1 relative">
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
        )}

        {/* Right: referral panel */}
        {panelOpen && selectedIds.size > 0 && (
          <ReferralPanel
            providers={selectedProviders}
            tenantId={tenantId}
            allCount={allCount}
            onClose={() => { setPanelOpen(false); setSelectedIds(new Set()); }}
          />
        )}
      </div>
    </div>
  );
}

// ── Provider row (compact list card) ─────────────────────────────────────────

const ProviderRow = forwardRef<
  HTMLDivElement,
  {
    provider: PublicProviderItem;
    number:   number;
    selected: boolean;
    hovered:  boolean;
    onHover:  (id: string | null) => void;
    onToggle: (id: string) => void;
  }
>(function ProviderRow({ provider, number, selected, hovered, onHover, onToggle }, ref) {
  const tags = [
    provider.primaryCategory,
  ].filter(Boolean) as string[];

  return (
    <div
      ref={ref}
      onMouseEnter={() => onHover(provider.id)}
      onMouseLeave={() => onHover(null)}
      className={[
        'flex items-start gap-2 px-3 py-2.5 cursor-default border-b border-gray-100 transition-colors',
        hovered   ? 'bg-blue-50'  : 'hover:bg-gray-50',
        selected  ? 'bg-blue-50'  : '',
      ].join(' ')}
    >
      {/* Content */}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-gray-900 leading-tight truncate">
          {provider.name}
        </p>
        <p className="text-xs text-gray-500 truncate mt-0.5">
          {provider.city}, {provider.state}
          {provider.primaryCategory ? ` · ${provider.primaryCategory}` : ''}
        </p>
        {tags.length > 0 && (
          <p className="text-xs text-gray-400 mt-0.5 truncate">
            {tags.join(' · ')}
          </p>
        )}
      </div>

      {/* Select toggle */}
      <button
        onClick={() => onToggle(provider.id)}
        className={[
          'flex-shrink-0 w-6 h-6 rounded flex items-center justify-center transition-colors mt-0.5',
          selected
            ? 'bg-blue-600 text-white'
            : 'bg-gray-100 text-gray-400 hover:bg-gray-200',
        ].join(' ')}
        title={selected ? 'Remove from selection' : 'Add to referral'}
      >
        <i className={selected ? 'ri-check-line text-xs' : 'ri-add-line text-xs'} />
      </button>
    </div>
  );
});

// ── Referral panel ────────────────────────────────────────────────────────────

interface ReferralForm {
  patientName:  string;
  patientPhone: string;
  notes:        string;
  firmName:     string;
  contactName:  string;
  email:        string;
  phone:        string;
}

const EMPTY_FORM: ReferralForm = {
  patientName: '', patientPhone: '', notes: '',
  firmName: '', contactName: '', email: '', phone: '',
};

type PanelState = 'form' | 'submitting' | 'success' | 'error';

function ReferralPanel({
  providers, tenantId, allCount, onClose,
}: {
  providers: PublicProviderItem[];
  tenantId:  string;
  allCount:  number;
  onClose:   () => void;
}) {
  const [form,        setForm]    = useState<ReferralForm>(EMPTY_FORM);
  const [state,       setState]   = useState<PanelState>('form');
  const [errorMsg,    setErrMsg]  = useState('');
  const [fieldErrors, setErrors]  = useState<Record<string, string>>({});
  const [openSection, setSection] = useState<'patient' | 'firm' | 'providers'>('patient');

  const update = useCallback((field: keyof ReferralForm, value: string) => {
    setForm(prev => ({ ...prev, [field]: value }));
    setErrors(prev => { const n = { ...prev }; delete n[field]; return n; });
  }, []);

  const handleSubmit = useCallback(async (e: FormEvent) => {
    e.preventDefault();
    setState('submitting');
    setErrors({});
    setErrMsg('');

    const [firstName, ...rest] = form.patientName.trim().split(' ');
    const lastName = rest.join(' ') || firstName;

    const payloads: PublicReferralRequest[] = providers.map(p => ({
      providerId:       p.id,
      senderName:       form.contactName || form.firmName,
      senderEmail:      form.email,
      patientFirstName: firstName,
      patientLastName:  lastName,
      patientPhone:     form.patientPhone,
      notes:            [
        form.notes,
        form.phone ? `Firm phone: ${form.phone}` : '',
        form.firmName ? `Firm: ${form.firmName}` : '',
      ].filter(Boolean).join('\n') || undefined,
    }));

    try {
      await Promise.all(payloads.map(payload =>
        fetch('/api/public/careconnect/api/public/referrals', {
          method:  'POST',
          headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': tenantId },
          body:    JSON.stringify(payload),
        }).then(res => {
          if (res.status === 422) return res.json().then(b => { throw b; });
          if (!res.ok)           return res.json().then(b => { throw b; }).catch(() => { throw new Error('Server error'); });
          return res.json();
        }),
      ));
      setState('success');
    } catch (err: unknown) {
      const msg = err && typeof err === 'object' && 'message' in err
        ? (err as { message: string }).message
        : 'Something went wrong. Please try again.';
      setErrMsg(msg);
      setState('error');
    }
  }, [form, providers, tenantId]);

  return (
    <div className="w-[320px] flex-shrink-0 border-l border-gray-200 bg-white flex flex-col overflow-hidden">
      {/* Panel header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
        <span className="text-sm font-semibold text-gray-800">
          Your selection{' '}
          <span className="inline-flex items-center justify-center w-5 h-5 rounded-full bg-gray-100 text-xs font-bold text-gray-700 ml-1">
            {providers.length}
          </span>
        </span>
        <button
          onClick={onClose}
          className="w-6 h-6 rounded flex items-center justify-center text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors"
        >
          <i className="ri-close-line text-sm" />
        </button>
      </div>

      {/* Panel body */}
      <div className="flex-1 overflow-y-auto">
        {state === 'success' ? (
          <div className="p-6 text-center space-y-4">
            <div className="mx-auto w-12 h-12 rounded-full bg-green-50 flex items-center justify-center">
              <i className="ri-checkbox-circle-line text-green-600 text-2xl" />
            </div>
            <div>
              <p className="text-sm font-semibold text-gray-900">Referrals sent!</p>
              <p className="text-xs text-gray-500 mt-1">
                Sent to {providers.length} provider{providers.length !== 1 ? 's' : ''}.
              </p>
            </div>
            <button
              onClick={onClose}
              className="px-4 py-2 text-xs font-semibold text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
            >
              Done
            </button>
          </div>
        ) : state === 'error' ? (
          <div className="p-6 text-center space-y-4">
            <div className="mx-auto w-12 h-12 rounded-full bg-red-50 flex items-center justify-center">
              <i className="ri-error-warning-line text-red-500 text-2xl" />
            </div>
            <div>
              <p className="text-sm font-semibold text-gray-900">Submission failed</p>
              <p className="text-xs text-gray-500 mt-1">{errorMsg}</p>
            </div>
            <button
              onClick={() => setState('form')}
              className="px-4 py-2 text-xs font-semibold text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
            >
              Try again
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit}>

            {/* ── Patient section ── */}
            <SectionRow
              avatar="P" avatarBg="bg-teal-500"
              title="Patient"
              subtitle="Who is being referred"
              open={openSection === 'patient'}
              onToggle={() => setSection(s => s === 'patient' ? 'firm' : 'patient')}
            >
              <div className="px-4 pb-3 space-y-2">
                <PanelField label="Patient name" required error={fieldErrors['patientName']}>
                  <input
                    type="text" required value={form.patientName}
                    placeholder="Jane Doe"
                    onChange={e => update('patientName', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(!!fieldErrors['patientName'])}
                  />
                </PanelField>
                <PanelField label="Patient contact" required error={fieldErrors['patientPhone']}>
                  <input
                    type="text" required value={form.patientPhone}
                    placeholder="555-123-4567 or email"
                    onChange={e => update('patientPhone', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(!!fieldErrors['patientPhone'])}
                  />
                </PanelField>
                <PanelField label="Notes" hint="optional">
                  <textarea
                    rows={3} value={form.notes}
                    placeholder="Background, urgency, prior treatment..."
                    onChange={e => update('notes', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(false) + ' resize-none'}
                  />
                </PanelField>
              </div>
            </SectionRow>

            {/* ── Law firm section ── */}
            <SectionRow
              avatar="L" avatarBg="bg-indigo-500"
              title="Law firm"
              subtitle="Who is sending the referral"
              open={openSection === 'firm'}
              onToggle={() => setSection(s => s === 'firm' ? 'patient' : 'firm')}
            >
              <div className="px-4 pb-3 space-y-2">
                <PanelField label="Firm name" required error={fieldErrors['firmName']}>
                  <input
                    type="text" required value={form.firmName}
                    placeholder="Acme Injury Law"
                    onChange={e => update('firmName', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(!!fieldErrors['firmName'])}
                  />
                </PanelField>
                <PanelField label="Contact name">
                  <input
                    type="text" value={form.contactName}
                    placeholder="Paralegal or attorney"
                    onChange={e => update('contactName', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(false)}
                  />
                </PanelField>
                <PanelField label="Email" required error={fieldErrors['email']}>
                  <input
                    type="email" required value={form.email}
                    placeholder="intake@firm.example"
                    onChange={e => update('email', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(!!fieldErrors['email'])}
                  />
                </PanelField>
                <PanelField label="Phone">
                  <input
                    type="tel" value={form.phone}
                    placeholder="(555) 555-5555"
                    onChange={e => update('phone', e.target.value)}
                    disabled={state === 'submitting'}
                    className={panelInputCls(false)}
                  />
                </PanelField>
              </div>
            </SectionRow>

            {/* ── Providers section ── */}
            <SectionRow
              avatar="P" avatarBg="bg-gray-700"
              title="Providers"
              subtitle="Who will treat the patient"
              badge={providers.length}
              open={openSection === 'providers'}
              onToggle={() => setSection(s => s === 'providers' ? 'patient' : 'providers')}
            >
              <div className="px-4 pb-3 space-y-1">
                {providers.map(p => (
                  <div key={p.id} className="flex items-center gap-2 py-1.5 border-b border-gray-100 last:border-0">
                    <div className="w-6 h-6 rounded-full bg-blue-100 flex items-center justify-center flex-shrink-0">
                      <i className="ri-hospital-line text-xs text-blue-600" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="text-xs font-medium text-gray-800 truncate">{p.name}</p>
                      <p className="text-xs text-gray-400 truncate">{p.city}, {p.state}</p>
                    </div>
                  </div>
                ))}
              </div>
            </SectionRow>

            {/* Submit */}
            <div className="px-4 py-4 border-t border-gray-100">
              <button
                type="submit"
                disabled={state === 'submitting'}
                className="w-full py-2 text-sm font-semibold text-white bg-blue-600 rounded-lg hover:bg-blue-700 disabled:opacity-60 transition-colors flex items-center justify-center gap-2"
              >
                {state === 'submitting' ? (
                  <><span className="w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin" />Sending…</>
                ) : (
                  <>Send Referral{providers.length > 1 ? `s (${providers.length})` : ''}</>
                )}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

// ── Section row ───────────────────────────────────────────────────────────────

function SectionRow({
  avatar, avatarBg, title, subtitle, badge, open, onToggle, children,
}: {
  avatar:    string;
  avatarBg:  string;
  title:     string;
  subtitle:  string;
  badge?:    number;
  open:      boolean;
  onToggle:  () => void;
  children:  ReactNode;
}) {
  return (
    <div className="border-b border-gray-100">
      <button
        type="button"
        onClick={onToggle}
        className="w-full flex items-center gap-3 px-4 py-3 text-left hover:bg-gray-50 transition-colors"
      >
        <div className={`w-8 h-8 rounded-full ${avatarBg} flex items-center justify-center flex-shrink-0`}>
          <span className="text-xs font-bold text-white">{avatar}</span>
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold text-gray-800">{title}</p>
          <p className="text-xs text-gray-400">{subtitle}</p>
        </div>
        {badge !== undefined && (
          <span className="w-5 h-5 rounded-full bg-gray-800 text-white text-xs font-bold flex items-center justify-center flex-shrink-0">
            {badge}
          </span>
        )}
        <i className={`ri-arrow-${open ? 'up' : 'down'}-s-line text-gray-400 text-sm flex-shrink-0`} />
      </button>
      {open && children}
    </div>
  );
}

// ── Panel field helpers ───────────────────────────────────────────────────────

function PanelField({
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

function panelInputCls(hasError: boolean) {
  return [
    'w-full rounded-lg border px-3 py-2 text-sm focus:outline-none focus:ring-1 transition-colors',
    hasError
      ? 'border-red-300 focus:border-red-400 focus:ring-red-100'
      : 'border-gray-200 focus:border-blue-400 focus:ring-blue-100',
  ].join(' ');
}

// ── Success / Error screens (kept for map popup referrals) ────────────────────
// (These are no longer used for the panel flow but kept for potential future use)

export type { PublicNetworkViewProps };
