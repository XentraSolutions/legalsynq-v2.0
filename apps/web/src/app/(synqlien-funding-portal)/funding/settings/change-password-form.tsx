"use client";

import { useRef, useState } from "react";

type Status = "idle" | "loading" | "success" | "error";

export function ChangePasswordForm() {
  const [status, setStatus] = useState<Status>("idle");
  const [message, setMessage] = useState("");
  const formRef = useRef<HTMLFormElement>(null);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setStatus("loading");
    setMessage("");

    const data = new FormData(event.currentTarget);
    const currentPassword = data.get("currentPassword") as string;
    const newPassword = data.get("newPassword") as string;
    const confirmPassword = data.get("confirmPassword") as string;

    if (!currentPassword || !newPassword || !confirmPassword) {
      setStatus("error");
      setMessage("All fields are required.");
      return;
    }

    if (newPassword.length < 8) {
      setStatus("error");
      setMessage("New password must be at least 8 characters.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setStatus("error");
      setMessage("New password and confirmation do not match.");
      return;
    }

    if (currentPassword === newPassword) {
      setStatus("error");
      setMessage("New password must differ from the current password.");
      return;
    }

    try {
      const response = await fetch("/api/auth/change-password", {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ currentPassword, newPassword }),
      });

      if (response.ok) {
        setStatus("success");
        setMessage("Password changed successfully.");
        formRef.current?.reset();
        return;
      }

      const body = await response.json().catch(() => ({}));
      const detail =
        body?.error ??
        body?.title ??
        body?.detail ??
        body?.message ??
        "Failed to change password. Please try again.";
      setStatus("error");
      setMessage(detail);
    } catch {
      setStatus("error");
      setMessage("Network error. Please check your connection and try again.");
    }
  }

  const loading = status === "loading";

  return (
    <form ref={formRef} onSubmit={handleSubmit} className="space-y-5">
      <PasswordField
        id="currentPassword"
        label="Current password"
        name="currentPassword"
        autoComplete="current-password"
        placeholder="Enter your current password"
        disabled={loading}
      />

      <div className="border-t border-[#f0f0f0]" />

      <PasswordField
        id="newPassword"
        label="New password"
        name="newPassword"
        autoComplete="new-password"
        placeholder="At least 8 characters"
        disabled={loading}
        minLength={8}
      />

      <PasswordField
        id="confirmPassword"
        label="Confirm new password"
        name="confirmPassword"
        autoComplete="new-password"
        placeholder="Repeat your new password"
        disabled={loading}
      />

      {message ? (
        <div
          role="status"
          className={`rounded-[8px] border px-4 py-3 text-[14px] font-normal leading-[1.6] ${
            status === "success"
              ? "border-[#86efac] bg-[#f0fdf4] text-[#15803d]"
              : "border-[#fecaca] bg-[#fef2f2] text-[#dc2626]"
          }`}
        >
          <i
            className={`mr-1.5 text-[16px] ${
              status === "success" ? "ri-checkbox-circle-line" : "ri-error-warning-line"
            }`}
            aria-hidden="true"
          />
          {message}
        </div>
      ) : null}

      <button
        type="submit"
        disabled={loading}
        className="inline-flex h-[38px] items-center justify-center rounded-[10px] bg-[#ee7132] px-5 text-[14px] font-medium leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:bg-[#d86228] disabled:bg-[#e5e5e5] disabled:text-[#737373]"
      >
        {loading ? "Saving..." : "Update password"}
      </button>
    </form>
  );
}

function PasswordField({
  id,
  label,
  name,
  autoComplete,
  placeholder,
  disabled,
  minLength,
}: {
  id: string;
  label: string;
  name: string;
  autoComplete: string;
  placeholder: string;
  disabled: boolean;
  minLength?: number;
}) {
  return (
    <div>
      <label
        htmlFor={id}
        className="mb-1.5 block text-[14px] font-medium leading-[1.6] text-[#525252]"
      >
        {label}
      </label>
      <input
        id={id}
        name={name}
        type="password"
        autoComplete={autoComplete}
        required
        minLength={minLength}
        disabled={disabled}
        placeholder={placeholder}
        className="h-10 w-full rounded-[8px] border border-[#e5e5e5] bg-white px-3 text-[14px] font-normal leading-[1.6] text-[#0a0a0a] shadow-[0_1px_1px_rgba(0,0,0,0.04)] outline-none transition placeholder:text-[#a3a3a3] focus:border-[#f4a076] focus:ring-2 focus:ring-[#fdf1eb] disabled:bg-[#f5f5f5] disabled:text-[#737373]"
      />
    </div>
  );
}
