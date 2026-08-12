'use client';

import { FormEvent, useCallback, useEffect, useRef, useState } from 'react';

type Fields = {
  tenantName: string;
  tenantCode: string;
  organizationType: string;
  streetAddress: string;
  adminFirstName: string;
  adminLastName: string;
  adminEmail: string;
};

interface AddressSuggestion {
  displayName: string;
  addressLine1: string;
  city: string;
  state: string;
  postalCode: string;
  latitude: number;
  longitude: number;
}

type SelectedAddress = {
  city: string;
  state: string;
  postalCode: string;
  latitude: number | null;
  longitude: number | null;
};

const initial: Fields = {
  tenantName: '',
  tenantCode: '',
  organizationType: '',
  streetAddress: '',
  adminFirstName: '',
  adminLastName: '',
  adminEmail: '',
};

const emptyAddress: SelectedAddress = {
  city: '',
  state: '',
  postalCode: '',
  latitude: null,
  longitude: null,
};

const organizationTypes = [
  { value: 'LAW_FIRM', label: 'Law Firm' },
  { value: 'PROVIDER', label: 'Provider' },
  { value: 'FUNDER', label: 'Funder' },
  { value: 'LIEN_OWNER', label: 'Lien Owner' },
];

function deriveTenantCode(name: string) {
  return name
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9\s-]/g, '')
    .replace(/[\s_]+/g, '-')
    .replace(/-{2,}/g, '-')
    .replace(/^-|-$/g, '')
    .slice(0, 63);
}

function getRegistrationErrorMessage(body: unknown, status: number) {
  if (status === 429) return 'Too many registration attempts. Please wait a few minutes and try again.';
  if (!body || typeof body !== 'object') return `Registration could not be submitted (HTTP ${status}).`;

  const payload = body as Record<string, unknown>;
  const nestedError = payload.error && typeof payload.error === 'object'
    ? payload.error as Record<string, unknown>
    : null;
  const candidates = [payload.message, nestedError?.message, payload.detail, payload.title, payload.error];
  const message = candidates.find(candidate => typeof candidate === 'string' && candidate.trim().length > 0);

  return typeof message === 'string'
    ? message
    : `Registration could not be submitted (HTTP ${status}).`;
}

export function RegistrationForm() {
  const [values, setValues] = useState(initial);
  const [address, setAddress] = useState(emptyAddress);
  const [addressSuggestions, setAddressSuggestions] = useState<AddressSuggestion[]>([]);
  const [addressLoading, setAddressLoading] = useState(false);
  const [addressMenuOpen, setAddressMenuOpen] = useState(false);
  const [addressIndex, setAddressIndex] = useState(-1);
  const [organizationMenuOpen, setOrganizationMenuOpen] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  function clearFieldError(key: string) {
    setErrors(current => {
      const next = { ...current };
      delete next[key];
      delete next.form;
      return next;
    });
  }
  const [submitting, setSubmitting] = useState(false);
  const [reference, setReference] = useState<string>();
  const summary = useRef<HTMLDivElement>(null);
  const addressInput = useRef<HTMLInputElement>(null);
  const addressMenu = useRef<HTMLDivElement>(null);
  const organizationSelect = useRef<HTMLDivElement>(null);
  const debounce = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    function closeMenus(event: MouseEvent) {
      const target = event.target as Node;
      if (!organizationSelect.current?.contains(target)) setOrganizationMenuOpen(false);
      if (!addressInput.current?.contains(target) && !addressMenu.current?.contains(target)) setAddressMenuOpen(false);
    }
    document.addEventListener('mousedown', closeMenus);
    return () => {
      document.removeEventListener('mousedown', closeMenus);
      if (debounce.current) clearTimeout(debounce.current);
    };
  }, []);

  const fetchAddressSuggestions = useCallback(async (query: string) => {
    if (query.trim().length < 3) {
      setAddressSuggestions([]);
      setAddressMenuOpen(false);
      return;
    }
    setAddressLoading(true);
    try {
      const response = await fetch(`/api/geocode/address?q=${encodeURIComponent(query)}`);
      if (!response.ok) return;
      const suggestions = await response.json() as AddressSuggestion[];
      setAddressSuggestions(suggestions);
      setAddressMenuOpen(suggestions.length > 0);
      setAddressIndex(-1);
    } catch {
      setAddressSuggestions([]);
    } finally {
      setAddressLoading(false);
    }
  }, []);

  function handleTenantName(name: string) {
    setValues(current => ({
      ...current,
      tenantName: name,
      tenantCode: current.tenantCode === deriveTenantCode(current.tenantName)
        ? deriveTenantCode(name)
        : current.tenantCode,
    }));
  }

  function handleStreetAddress(streetAddress: string) {
    setValues(current => ({ ...current, streetAddress }));
    setAddress(emptyAddress);
    if (debounce.current) clearTimeout(debounce.current);
    debounce.current = setTimeout(() => fetchAddressSuggestions(streetAddress), 300);
  }

  function selectAddress(suggestion: AddressSuggestion) {
    const typedNumber = values.streetAddress.trim().match(/^(\d+[-\w]*)\s+/);
    const addressLine1 = typedNumber && !/^\d/.test(suggestion.addressLine1)
      ? `${typedNumber[1]} ${suggestion.addressLine1}`
      : suggestion.addressLine1;
    setValues(current => ({ ...current, streetAddress: addressLine1 }));
    setAddress({
      city: suggestion.city,
      state: suggestion.state,
      postalCode: suggestion.postalCode,
      latitude: suggestion.latitude,
      longitude: suggestion.longitude,
    });
    setAddressSuggestions([]);
    setAddressMenuOpen(false);
  }

  function handleAddressKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (!addressMenuOpen || addressSuggestions.length === 0) return;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setAddressIndex(index => Math.min(index + 1, addressSuggestions.length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setAddressIndex(index => Math.max(index - 1, 0));
    } else if (event.key === 'Enter' && addressIndex >= 0) {
      event.preventDefault();
      selectAddress(addressSuggestions[addressIndex]);
    } else if (event.key === 'Escape') {
      setAddressMenuOpen(false);
    }
  }

  function validate() {
    const next: Record<string, string> = {};
    for (const key of ['tenantName', 'tenantCode', 'organizationType', 'adminFirstName', 'adminLastName', 'adminEmail'] as const) {
      if (!values[key].trim()) next[key] = 'This field is required.';
    }
    if (values.adminEmail && !/^\S+@\S+\.\S+$/.test(values.adminEmail)) next.adminEmail = 'Enter a valid email address.';
    if (values.tenantCode && !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(values.tenantCode)) next.tenantCode = 'Use lowercase letters, numbers, and single hyphens.';
    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!validate()) {
      setTimeout(() => summary.current?.focus(), 0);
      return;
    }
    setSubmitting(true);
    setErrors({});
    try {
      const streetAddress = [
        values.streetAddress,
        address.city,
        address.postalCode ? `${address.state} ${address.postalCode}` : address.state,
      ].filter(Boolean).join(', ');
      const response = await fetch('/api/tenant/api/v1/public/tenant-registrations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...values, streetAddress, addressLine1: values.streetAddress, addressCity: address.city, addressState: address.state, addressPostalCode: address.postalCode }),
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(getRegistrationErrorMessage(body, response.status));
      setReference(body.registrationId);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Registration could not be submitted.';
      // Only an Identity account collision belongs to the email field. Pending
      // registration conflicts (including email conflicts) stay in the alert
      // above the form so the backend's exact message remains visible there.
      const isExistingIdentityAccount = /^An account with this administrator email already exists\.$/i.test(message);
      setErrors(isExistingIdentityAccount ? { adminEmail: message } : { form: message });
      setTimeout(() => summary.current?.focus(), 0);
    } finally {
      setSubmitting(false);
    }
  }

  if (reference) return <section className="w-full rounded-2xl border border-[#e5e5e5] bg-white pt-16 text-center shadow-[0_1px_3px_rgba(0,0,0,0.1)]">
    <div className="flex flex-col items-center gap-10 px-6 pb-6">
      <div className="registration-success-breathe flex size-20 items-center justify-center rounded-full bg-[rgba(34,197,94,0.15)]">
        <div className="flex size-12 items-center justify-center rounded-full bg-[#22c55e] text-white">
          <i aria-hidden="true" className="ri-check-line text-2xl leading-none" />
        </div>
      </div>

      <div className="flex w-full flex-col items-center gap-4">
        <span className="flex h-7 items-center justify-center rounded-2xl bg-[rgba(234,179,8,0.05)] px-3 py-1 text-sm font-medium leading-[22px] text-[#a16207]">Application Pending</span>
        <h1 className="w-full text-xl font-semibold leading-7 text-[#0a0a0a]">Registration Submitted!</h1>
        <p className="w-full text-base font-normal leading-[25.6px] text-[#737373]">
          Thank you for registering <strong className="font-semibold text-[#404040]">{values.tenantName}</strong>. Our team will review your application and you will receive an email notification once your organization has been approved.
        </p>
      </div>
    </div>

    <div className="p-6">
      <button
        type="button"
        onClick={() => {
          setValues(initial);
          setAddress(emptyAddress);
          setAddressSuggestions([]);
          setErrors({});
          setReference(undefined);
        }}
        className="h-[38px] w-full rounded-[10px] bg-[#ee7132] px-4 text-sm font-medium leading-[22px] text-white transition hover:bg-[#d95f24] focus:outline-none focus-visible:ring-2 focus-visible:ring-[#ee7132]/30 focus-visible:ring-offset-2"
      >
        Register New Tenant
      </button>
    </div>
  </section>;

  const field = (key: keyof Fields, label: string, placeholder: string, required = true, type = 'text') => <label className="block text-sm font-medium leading-[22px]">
    {label}{required && <span className="ml-1 text-red-500">*</span>}
    <input
      type={type}
      value={values[key]}
      placeholder={placeholder}
      onChange={event => key === 'tenantName'
        ? handleTenantName(event.target.value)
        : key === 'tenantCode'
          ? setValues(current => ({ ...current, tenantCode: event.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '').replace(/^-/, '') }))
        : setValues(current => ({ ...current, [key]: event.target.value }))}
      aria-invalid={!!errors[key]}
      aria-describedby={`${key}-error`}
      className={`mt-3 h-9 w-full rounded-lg border bg-white px-3 text-sm font-normal outline-none transition placeholder:text-[#737373] focus:ring-2 focus:ring-[#ee7132]/20 ${errors[key] ? 'border-red-500' : 'border-[#e5e5e5] focus:border-[#ee7132]'}`}
    />
    {errors[key] && <span id={`${key}-error`} className="mt-1 block text-xs font-normal text-red-600">{errors[key]}</span>}
  </label>;

  const selectedOrganization = organizationTypes.find(type => type.value === values.organizationType);

  return <form onSubmit={submit} noValidate className="w-full rounded-2xl border border-[#e5e5e5] bg-white py-6 shadow-[0_1px_3px_rgba(0,0,0,0.1)]">
    <div className="px-6 pb-4 text-center">
      <h1 className="text-xl font-semibold leading-7">Register Your Tenant</h1>
      <p className="mt-2 text-sm leading-[22px] text-[#737373]">Fill in the required details to get started.</p>
    </div>

    {Object.keys(errors).length > 0 && <div ref={summary} tabIndex={-1} role="alert" className="mx-6 mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{errors.form ?? 'Please correct the highlighted fields.'}</div>}

    <section aria-labelledby="tenant-information-heading" className="mx-6 border-b border-[#e5e5e5] pb-6 pt-4">
      <p id="tenant-information-heading" className="mb-4 text-sm font-normal uppercase leading-[22px] text-[#737373]">Tenant information</p>
      <div className="space-y-6">
        {field('tenantName', 'Tenant Name', 'e.g. Acme Law Group')}
        <div>
          {field('tenantCode', 'Tenant Code', 'e.g. acme-law')}
          {!errors.tenantCode && <p className="mt-3 text-sm font-normal leading-[22px] text-[#737373]">Lowercase letters, numbers, and hyphens. This will also be the tenant&apos;s subdomain(...nonprod.legalsynq.net). Cannot be changed later.</p>}
        </div>
        <div ref={organizationSelect} className="relative">
          <label id="organization-type-label" className="block text-sm font-medium leading-[22px]">Organization Type <span className="text-red-500">*</span></label>
          <button
            type="button"
            aria-labelledby="organization-type-label"
            aria-haspopup="listbox"
            aria-expanded={organizationMenuOpen}
            onClick={() => setOrganizationMenuOpen(open => !open)}
            className={`mt-3 flex h-9 w-full items-center rounded-lg border bg-white px-3 text-left text-sm font-normal outline-none transition focus:ring-2 focus:ring-[#ee7132]/20 ${errors.organizationType ? 'border-red-500' : 'border-[#e5e5e5] focus:border-[#ee7132]'}`}
          >
            <span className={`flex-1 ${selectedOrganization ? 'text-[#0a0a0a]' : 'text-[#737373]'}`}>{selectedOrganization?.label ?? 'Select organization type'}</span>
            <svg aria-hidden="true" viewBox="0 0 16 16" className={`size-4 transition-transform ${organizationMenuOpen ? 'rotate-180' : ''}`} fill="none"><path d="m4 6 4 4 4-4" stroke="currentColor" strokeWidth="1.25" strokeLinecap="round" strokeLinejoin="round" /></svg>
          </button>
          {organizationMenuOpen && <div role="listbox" aria-labelledby="organization-type-label" className="absolute left-0 right-0 top-[76px] z-30 rounded-lg border border-[#e5e5e5] bg-white p-1 shadow-[0_4px_6px_-1px_rgba(0,0,0,0.1),0_2px_4px_-2px_rgba(0,0,0,0.1)]">
            {organizationTypes.map(type => <button
              key={type.value}
              type="button"
              role="option"
              aria-selected={values.organizationType === type.value}
              onClick={() => {
                setValues(current => ({ ...current, organizationType: type.value }));
                clearFieldError('organizationType');
                setOrganizationMenuOpen(false);
              }}
              className={`block w-full rounded px-2 py-1.5 text-left text-sm leading-[22px] transition hover:bg-[#f5f5f5] ${values.organizationType === type.value ? 'bg-[#ee7132]/5 text-[#ee7132]' : 'text-[#0a0a0a]'}`}
            >{type.label}</button>)}
          </div>}
          {errors.organizationType && <span className="mt-1 block text-xs text-red-600">{errors.organizationType}</span>}
        </div>
      </div>
    </section>

    <section aria-labelledby="address-heading" className="mx-6 border-b border-[#e5e5e5] pb-6 pt-6">
      <p id="address-heading" className="mb-4 text-sm font-normal uppercase leading-[22px] text-[#737373]">Address (Optional)</p>
      <div className="relative">
        <label htmlFor="street-address" className="block text-sm font-medium leading-[22px]">Street Name</label>
        <div className="relative mt-3">
          <input
            ref={addressInput}
            id="street-address"
            type="text"
            autoComplete="off"
            value={values.streetAddress}
            onChange={event => handleStreetAddress(event.target.value)}
            onKeyDown={handleAddressKeyDown}
            onFocus={() => addressSuggestions.length > 0 && setAddressMenuOpen(true)}
            placeholder="e.g. 123 Main street..."
            className="h-9 w-full rounded-lg border border-[#e5e5e5] bg-white px-3 pr-9 text-sm font-normal outline-none placeholder:text-[#737373] focus:border-[#ee7132] focus:ring-2 focus:ring-[#ee7132]/20"
          />
          {addressLoading && <span aria-label="Looking up address" className="absolute right-3 top-1/2 block size-4 -translate-y-1/2 animate-spin rounded-full border-2 border-[#d4d4d4] border-t-[#ee7132]" />}
          {!addressLoading && address.latitude !== null && <span aria-label="Address selected" className="absolute right-3 top-1/2 -translate-y-1/2 text-[#22c55e]">✓</span>}
        </div>
        {addressMenuOpen && addressSuggestions.length > 0 && <div ref={addressMenu} role="listbox" className="absolute left-0 right-0 top-[76px] z-20 overflow-hidden rounded-lg border border-[#e5e5e5] bg-white shadow-lg">
          {addressSuggestions.map((suggestion, index) => <button
            key={suggestion.displayName}
            type="button"
            role="option"
            aria-selected={index === addressIndex}
            onMouseDown={event => { event.preventDefault(); selectAddress(suggestion); }}
            className={`block w-full px-3 py-2 text-left text-xs transition hover:bg-[#ee7132]/5 ${index === addressIndex ? 'bg-[#ee7132]/5 text-[#ee7132]' : 'text-[#0a0a0a]'}`}
          >
            <span className="font-medium">{suggestion.addressLine1}</span>
            <span className="ml-1 text-[#737373]">{suggestion.city}, {suggestion.state} {suggestion.postalCode}</span>
          </button>)}
        </div>}
      </div>
      {address.latitude !== null && <div className="mt-4 grid grid-cols-3 gap-3">
        <label className="block text-sm font-medium leading-[22px]">City<input value={address.city} onChange={event => setAddress(current => ({ ...current, city: event.target.value }))} className="mt-3 h-9 w-full rounded-lg border border-[#e5e5e5] px-3 text-sm font-normal outline-none focus:border-[#ee7132]" /></label>
        <label className="block text-sm font-medium leading-[22px]">State<input value={address.state} maxLength={2} onChange={event => setAddress(current => ({ ...current, state: event.target.value.toUpperCase() }))} className="mt-3 h-9 w-full rounded-lg border border-[#e5e5e5] px-3 text-sm font-normal uppercase outline-none focus:border-[#ee7132]" /></label>
        <label className="block text-sm font-medium leading-[22px]">ZIP<input value={address.postalCode} maxLength={10} onChange={event => setAddress(current => ({ ...current, postalCode: event.target.value }))} className="mt-3 h-9 w-full rounded-lg border border-[#e5e5e5] px-3 text-sm font-normal outline-none focus:border-[#ee7132]" /></label>
      </div>}
    </section>

    <section aria-labelledby="admin-user-heading" className="px-6 pb-5 pt-6">
      <p id="admin-user-heading" className="mb-4 text-sm font-normal uppercase leading-[22px] text-[#737373]">Default Admin User</p>
      <div className="space-y-6">
        <div className="grid grid-cols-2 gap-3">{field('adminFirstName', 'First Name', 'e.g. John')}{field('adminLastName', 'Last Name', 'e.g. Doe')}</div>
        {field('adminEmail', 'Email Address', 'e.g. john.doe@acme.com', true, 'email')}
      </div>
    </section>

    <div className="px-6 pt-5">
      <button disabled={submitting} className="h-[38px] w-full rounded-[10px] bg-[#ee7132] px-4 text-sm font-medium text-white transition hover:bg-[#d95f24] disabled:cursor-not-allowed disabled:opacity-60">{submitting ? 'Registering tenant…' : 'Register Tenant'}</button>
    </div>
  </form>;
}
