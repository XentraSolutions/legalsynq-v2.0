"use client";

import { useState, type FormEvent } from "react";
import type { PublicBuyerPortalData } from "@/lib/liens/public-buyer-portal";
import {
  activatePublicBuyerPortalAccount,
  type PublicBuyerPortalActivationData,
} from "@/lib/liens/public-buyer-portal-activation";
import { formatPhoneInput, isValidPhone, toE164Phone } from "@/lib/phone";

interface PublicBuyerActivationFormProps {
  token: string;
  data: PublicBuyerPortalData;
}

export function PublicBuyerActivationForm({
  token,
  data,
}: PublicBuyerActivationFormProps) {
  const buyerName = splitName(data.buyer.contactName);
  const initialCompanyName = data.buyer.company ?? "";
  const initialEmail = data.buyer.email ?? "";
  const initialFirstName = buyerName.firstName;
  const initialLastName = buyerName.lastName;
  const initialPhone = formatPhoneInput(data.buyer.phone ?? "");

  const [companyName, setCompanyName] = useState(initialCompanyName);
  const [email, setEmail] = useState(initialEmail);
  const [firstName, setFirstName] = useState(initialFirstName);
  const [lastName, setLastName] = useState(initialLastName);
  const [phone, setPhone] = useState(initialPhone);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [agreeTerms, setAgreeTerms] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [activation, setActivation] = useState<PublicBuyerPortalActivationData | null>(null);

  const companyNameLocked = Boolean(initialCompanyName.trim());
  const emailLocked = Boolean(initialEmail.trim());
  const firstNameLocked = Boolean(initialFirstName.trim());
  const lastNameLocked = Boolean(initialLastName.trim());
  const phoneLocked = Boolean(initialPhone.trim());
  const hasPhoneValue = phone.trim().length > 0;
  const hasInvalidPhone = hasPhoneValue && !isValidPhone(phone);

  function validate(): string | null {
    if (!companyName.trim()) return "Company name is required.";
    if (!email.trim()) return "Email address is required.";
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) return "Enter a valid email address.";
    if (!firstName.trim()) return "First name is required.";
    if (hasInvalidPhone) return "Phone number must be 10 digits.";
    if (!password) return "Password is required.";
    if (password.length < 8) return "Password must be at least 8 characters.";
    if (password !== confirmPassword) return "Passwords do not match.";
    if (!agreeTerms) return "You must agree to the Terms of Service and Privacy Policy to continue.";
    return null;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitError("");

    const validationError = validate();
    if (validationError) {
      setSubmitError(validationError);
      return;
    }

    setSubmitting(true);
    const result = await activatePublicBuyerPortalAccount(token, {
      companyName: companyName.trim(),
      email: email.trim(),
      firstName: firstName.trim(),
      lastName: lastName.trim() || undefined,
      phone: toE164Phone(phone),
      password,
    });
    setSubmitting(false);

    if (result.ok) {
      setActivation(result.data);
      return;
    }

    setSubmitError(result.error.message);
  }

  if (activation) {
    return (
      <section
        className="flex w-full max-w-[700px] flex-col gap-5 rounded-2xl border border-[#d1fae5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] max-sm:rounded-[14px]"
        aria-labelledby="activation-success-title"
      >
        <div className="flex items-start gap-4">
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-emerald-500/15 text-emerald-700">
            <i className="ri-shield-check-line text-2xl leading-none" aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <h2 id="activation-success-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal text-[#0a0a0a]">
              Account activated
            </h2>
            <p className="m-0 text-sm leading-[1.6] text-[#737373]">
              {activation.isNew
                ? "Your SynqLien buyer account was created."
                : "Your existing account now has SynqLien buyer access."}
            </p>
          </div>
        </div>
        <a
          href={activation.loginUrl || "/login?returnTo=%2Ffunding%2Foffered-liens"}
          className="public-portal-primary inline-flex h-11 items-center justify-center rounded-[10px] px-4 py-2 text-sm font-semibold leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:shadow-[0_4px_10px_rgba(238,113,50,0.24)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]"
        >
          Log in to Manage Liens
        </a>
      </section>
    );
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="flex w-full max-w-[700px] flex-col gap-6 rounded-2xl border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] max-sm:rounded-[14px]"
      aria-labelledby="activation-form-title"
    >
      <div className="flex flex-col gap-2">
        <h2 id="activation-form-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal">
          Create Portal Login
        </h2>
        <p className="m-0 text-base leading-[1.6] text-[#737373]">
          Confirm the buyer contact details and create a password for your funding company account.
        </p>
      </div>

      <section className="flex flex-col gap-4" aria-labelledby="organization-section-title">
        <h3 id="organization-section-title" className="m-0 text-sm font-bold uppercase leading-[1.6] tracking-normal text-[#737373]">
          Organization
        </h3>
        <div className="grid grid-cols-2 gap-4 max-sm:grid-cols-1">
          <Field
            id="buyer-company"
            label="Company Name"
            value={companyName}
            onChange={setCompanyName}
            disabled={companyNameLocked}
            required
            placeholder="Enter company name"
          />
          <Field
            id="buyer-email"
            label="Email Address"
            type="email"
            value={email}
            onChange={setEmail}
            disabled={emailLocked}
            required
            placeholder="Enter email address"
          />
        </div>
      </section>

      <section className="flex flex-col gap-4" aria-labelledby="contact-section-title">
        <h3 id="contact-section-title" className="m-0 text-sm font-bold uppercase leading-[1.6] tracking-normal text-[#737373]">
          Contact
        </h3>
        <div className="grid grid-cols-2 gap-4 max-sm:grid-cols-1">
          <Field
            id="buyer-first-name"
            label="First Name"
            value={firstName}
            onChange={setFirstName}
            disabled={firstNameLocked}
            required
            placeholder="Enter first name"
          />
          <Field
            id="buyer-last-name"
            label="Last Name"
            value={lastName}
            onChange={setLastName}
            disabled={lastNameLocked}
            placeholder="Enter last name"
          />
        </div>
        <div>
          <Field
            id="buyer-phone"
            label="Phone Number"
            type="tel"
            value={phone}
            onChange={value => setPhone(formatPhoneInput(value))}
            disabled={phoneLocked}
            placeholder="Enter 10-digit phone number"
            invalid={hasInvalidPhone}
          />
          {hasInvalidPhone ? (
            <p className="m-0 mt-1 text-xs font-semibold leading-[1.6] text-red-600">
              Phone number must be 10 digits.
            </p>
          ) : null}
        </div>
      </section>

      <section className="flex flex-col gap-4" aria-labelledby="account-section-title">
        <h3 id="account-section-title" className="m-0 text-sm font-bold uppercase leading-[1.6] tracking-normal text-[#737373]">
          Account
        </h3>
        <PasswordField
          id="buyer-password"
          label="Password"
          value={password}
          onChange={setPassword}
          show={showPassword}
          onToggleShow={() => setShowPassword(value => !value)}
        />
        <PasswordField
          id="buyer-confirm-password"
          label="Confirm Password"
          value={confirmPassword}
          onChange={setConfirmPassword}
          show={showConfirmPassword}
          onToggleShow={() => setShowConfirmPassword(value => !value)}
          invalid={Boolean(confirmPassword && password !== confirmPassword)}
        />
        {confirmPassword && password !== confirmPassword ? (
          <p className="m-0 text-xs font-semibold leading-[1.6] text-red-600">
            Passwords do not match.
          </p>
        ) : null}
      </section>

      <div className="flex items-start gap-3">
        <input
          id="buyer-agree-terms"
          type="checkbox"
          checked={agreeTerms}
          onChange={event => setAgreeTerms(event.target.checked)}
          className="mt-1 h-4 w-4 rounded border-[#d4d4d4] text-[#ee7132] focus:ring-[#ee7132]"
        />
        <label htmlFor="buyer-agree-terms" className="text-sm leading-[1.6] text-[#737373]">
          I agree to the{" "}
          <a href="/coming-soon" target="_blank" className="text-[#ee7132] underline underline-offset-2">
            Terms of Service
          </a>{" "}
          and{" "}
          <a href="/coming-soon" target="_blank" className="text-[#ee7132] underline underline-offset-2">
            Privacy Policy
          </a>
          . I confirm I am authorized to create this account for the buyer organization.
        </label>
      </div>

      {submitError ? (
        <div
          role="alert"
          className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-semibold leading-[1.6] text-red-700"
        >
          <i className="ri-error-warning-line mt-0.5 shrink-0 leading-none" aria-hidden="true" />
          <span>{submitError}</span>
        </div>
      ) : null}

      <button
        type="submit"
        disabled={submitting}
        className="public-portal-primary inline-flex h-11 cursor-pointer items-center justify-center gap-2 rounded-[10px] border border-transparent px-4 py-2 text-sm font-semibold leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:shadow-[0_4px_10px_rgba(238,113,50,0.24)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] disabled:cursor-not-allowed disabled:opacity-60"
      >
        {submitting ? (
          <>
            <i className="ri-loader-4-line animate-spin leading-none" aria-hidden="true" />
            Creating Account...
          </>
        ) : (
          <>
            <i className="ri-shield-check-line leading-none" aria-hidden="true" />
            Activate Free Account
          </>
        )}
      </button>
    </form>
  );
}

function Field({
  id,
  label,
  value,
  onChange,
  type = "text",
  disabled = false,
  required = false,
  placeholder,
  invalid = false,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  disabled?: boolean;
  required?: boolean;
  placeholder?: string;
  invalid?: boolean;
}) {
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold leading-[1.6] text-[#0a0a0a]">
        {label}
        {required ? <span className="text-red-600"> *</span> : null}
      </label>
      <input
        id={id}
        type={type}
        value={value}
        onChange={event => !disabled && onChange(event.target.value)}
        disabled={disabled}
        required={required}
        placeholder={placeholder}
        className={[
          "h-10 w-full rounded-[10px] border px-3 text-sm leading-[1.6] outline-none transition-colors focus:ring-2 focus:ring-[#ee7132]/30",
          disabled
            ? "cursor-not-allowed border-[#e5e5e5] bg-[#f5f5f5] text-[#737373]"
            : invalid
              ? "border-red-300 bg-white text-[#0a0a0a] focus:border-red-500"
              : "border-[#d4d4d4] bg-white text-[#0a0a0a] focus:border-[#ee7132]",
        ].join(" ")}
      />
    </div>
  );
}

function PasswordField({
  id,
  label,
  value,
  onChange,
  show,
  onToggleShow,
  invalid = false,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  show: boolean;
  onToggleShow: () => void;
  invalid?: boolean;
}) {
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold leading-[1.6] text-[#0a0a0a]">
        {label}
        <span className="text-red-600"> *</span>
      </label>
      <div className="relative">
        <input
          id={id}
          type={show ? "text" : "password"}
          value={value}
          onChange={event => onChange(event.target.value)}
          required
          minLength={8}
          autoComplete="new-password"
          placeholder={label}
          className={[
            "h-10 w-full rounded-[10px] border bg-white px-3 pr-11 text-sm leading-[1.6] text-[#0a0a0a] outline-none transition-colors focus:ring-2 focus:ring-[#ee7132]/30",
            invalid ? "border-red-300 focus:border-red-500" : "border-[#d4d4d4] focus:border-[#ee7132]",
          ].join(" ")}
        />
        <button
          type="button"
          onClick={onToggleShow}
          aria-label={show ? `Hide ${label.toLowerCase()}` : `Show ${label.toLowerCase()}`}
          className="absolute right-2 top-1/2 flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded-lg text-[#737373] transition-colors hover:bg-[#f5f5f5] hover:text-[#333] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132]"
        >
          <i className={show ? "ri-eye-off-line" : "ri-eye-line"} aria-hidden="true" />
        </button>
      </div>
    </div>
  );
}

function splitName(value: string | null | undefined): { firstName: string; lastName: string } {
  const trimmed = value?.trim() ?? "";
  if (!trimmed) return { firstName: "", lastName: "" };

  const parts = trimmed.split(/\s+/).filter(Boolean);
  return {
    firstName: parts[0] ?? "",
    lastName: parts.slice(1).join(" "),
  };
}
