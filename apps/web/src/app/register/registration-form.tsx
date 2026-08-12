'use client';

import { FormEvent, useRef, useState } from 'react';

type Fields = { tenantName: string; tenantCode: string; organizationType: string; streetAddress: string; adminFirstName: string; adminLastName: string; adminEmail: string };
const initial: Fields = { tenantName: '', tenantCode: '', organizationType: '', streetAddress: '', adminFirstName: '', adminLastName: '', adminEmail: '' };

export function RegistrationForm() {
  const [values, setValues] = useState(initial);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const [reference, setReference] = useState<string>();
  const summary = useRef<HTMLDivElement>(null);

  function validate() {
    const next: Record<string, string> = {};
    for (const key of ['tenantName','tenantCode','organizationType','adminFirstName','adminLastName','adminEmail'] as const)
      if (!values[key].trim()) next[key] = 'This field is required.';
    if (values.adminEmail && !/^\S+@\S+\.\S+$/.test(values.adminEmail)) next.adminEmail = 'Enter a valid email address.';
    if (values.tenantCode && !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(values.tenantCode)) next.tenantCode = 'Use lowercase letters, numbers, and single hyphens.';
    setErrors(next); return Object.keys(next).length === 0;
  }

  async function submit(event: FormEvent) {
    event.preventDefault(); if (!validate()) { setTimeout(() => summary.current?.focus(), 0); return; }
    setSubmitting(true); setErrors({});
    try {
      const response = await fetch('/api/tenant/api/v1/public/tenant-registrations', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(values) });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(body.message ?? body.error ?? 'Registration could not be submitted.');
      setReference(body.registrationId);
    } catch (error) { setErrors({ form: error instanceof Error ? error.message : 'Registration could not be submitted.' }); setTimeout(() => summary.current?.focus(), 0); }
    finally { setSubmitting(false); }
  }

  if (reference) return <section className="w-full rounded-2xl border border-[#e5e5e5] bg-white p-8 text-center shadow-sm">
    <div className="mx-auto mb-5 flex size-12 items-center justify-center rounded-full bg-[#fff3ed] text-2xl text-[#ee7132]">✓</div>
    <h1 className="text-xl font-semibold">Registration Submitted</h1><p className="mt-2 font-medium text-[#ee7132]">Pending Review</p>
    <p className="mt-4 text-sm leading-6 text-[#737373]">No tenant or DNS record has been created yet. The administrator will receive an email after the application is approved or declined.</p>
    <p className="mt-6 rounded-lg bg-[#f5f5f5] p-3 text-xs text-[#525252]">Registration reference: <span className="font-mono">{reference}</span></p>
  </section>;

  const field = (key: keyof Fields, label: string, required = true, type = 'text', hint?: string) => <label className="block text-sm font-medium">{label}{required && <span className="ml-1 text-red-500">*</span>}
    <input type={type} value={values[key]} onChange={e => setValues(v => ({ ...v, [key]: e.target.value }))} aria-invalid={!!errors[key]} aria-describedby={`${key}-error`} className={`mt-2 h-10 w-full rounded-lg border bg-white px-3 font-normal outline-none transition focus:ring-2 focus:ring-[#ee7132]/25 ${errors[key] ? 'border-red-500' : 'border-[#e5e5e5] focus:border-[#ee7132]'}`} />
    {hint && !errors[key] && <span className="mt-1 block text-xs font-normal leading-5 text-[#737373]">{hint}</span>}{errors[key] && <span id={`${key}-error`} className="mt-1 block text-xs font-normal text-red-600">{errors[key]}</span>}
  </label>;

  return <form onSubmit={submit} noValidate className="w-full rounded-2xl border border-[#e5e5e5] bg-white p-6 shadow-sm">
    <div className="mb-6 text-center"><h1 className="text-xl font-semibold">Register Your Tenant</h1><p className="mt-2 text-sm text-[#737373]">Fill in the required details to get started.</p></div>
    {Object.keys(errors).length > 0 && <div ref={summary} tabIndex={-1} role="alert" className="mb-5 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{errors.form ?? 'Please correct the highlighted fields.'}</div>}
    <fieldset className="space-y-5"><legend className="mb-4 text-xs font-normal uppercase text-[#737373]">Tenant information</legend>
      {field('tenantName','Tenant Name')}{field('tenantCode','Tenant Code',true,'text',"Lowercase letters, numbers, and hyphens. This becomes the tenant's subdomain and cannot be changed later.")}
      <label className="block text-sm font-medium">Organization Type <span className="text-red-500">*</span><select value={values.organizationType} onChange={e=>setValues(v=>({...v,organizationType:e.target.value}))} className="mt-2 h-10 w-full rounded-lg border border-[#e5e5e5] bg-white px-3 font-normal"><option value="">Select organization type</option><option value="LAW_FIRM">Law Firm</option><option value="MEDICAL_PROVIDER">Medical Provider</option><option value="FUNDING_COMPANY">Funding Company</option></select>{errors.organizationType && <span className="mt-1 block text-xs text-red-600">{errors.organizationType}</span>}</label>
      {field('streetAddress','Street Address',false)}
    </fieldset><div className="my-6 border-t border-[#e5e5e5]" />
    <fieldset className="space-y-5"><legend className="mb-4 text-xs font-normal uppercase text-[#737373]">Administrator information</legend>
      <div className="grid gap-4 sm:grid-cols-2">{field('adminFirstName','First Name')}{field('adminLastName','Last Name')}</div>{field('adminEmail','Email Address',true,'email')}
    </fieldset>
    <button disabled={submitting} className="mt-7 h-10 w-full rounded-[10px] bg-[#ee7132] px-4 text-sm font-medium text-white transition hover:bg-[#d95f24] disabled:cursor-not-allowed disabled:opacity-60">{submitting ? 'Submitting registration…' : 'Submit Registration'}</button>
  </form>;
}
