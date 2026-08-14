"use client";
import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import type { TenantRegistration } from "@/lib/control-center-api";
import { NotificationBanner } from "@/components/ui/notification-banner";
import { approveRegistration, declineRegistration } from "../actions";
import { TenantApplicationDialog } from "../tenant-application-dialog";

export function DecisionPanel({
  application,
}: {
  application: TenantRegistration;
}) {
  const router = useRouter();
  const [mode, setMode] = useState<"approve" | "decline" | null>(null);
  const [error, setError] = useState("");
  const [notification, setNotification] = useState<
    "approve" | "decline" | null
  >(null);
  const [pending, start] = useTransition();
  const submit = () => {
    if (!mode) return;
    start(async () => {
      try {
        if (mode === "approve") await approveRegistration(application.id);
        else
          await declineRegistration(
            application.id,
            "Declined by platform administrator.",
          );
        const completed = mode;
        setMode(null);
        setNotification(completed);
        router.refresh();
      } catch (e) {
        setError(e instanceof Error ? e.message : "The request failed.");
      }
    });
  };
  return (
    <>
      {application.registrationStatus === "PendingReview" && (
        <div className="flex gap-2">
          <button
            onClick={() => setMode("approve")}
            className="rounded-lg bg-[#16a34a] px-4 py-2 text-sm font-medium text-white"
          >
            Accept
          </button>
          <button
            onClick={() => setMode("decline")}
            className="rounded-lg border border-[#dc2626] px-4 py-2 text-sm font-medium text-[#dc2626]"
          >
            Decline
          </button>
        </div>
      )}
      {mode && (
        <TenantApplicationDialog
          tenantName={application.tenantName}
          decision={mode}
          pending={pending}
          error={error}
          onConfirm={submit}
          onCancel={() => {
            setMode(null);
            setError("");
          }}
        />
      )}
      {notification && (
        <NotificationBanner
          title={`Tenant ${notification === "approve" ? "Approved" : "Declined"}!`}
          description={`Tenant registration has been successfully ${notification === "approve" ? "approved" : "declined"}.`}
          onDismiss={() => setNotification(null)}
        />
      )}
    </>
  );
}
