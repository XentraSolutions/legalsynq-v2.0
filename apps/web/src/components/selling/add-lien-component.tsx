import React, { useEffect, useMemo, useState } from "react";
import { ApiError } from "@/lib/api-client";
import LienInfo from "./forms/add-medical-lien/lien-info";
import FundingCompanyInfo from "./forms/add-medical-lien/funding-company-info";
import MedicalCodesDescription from "./forms/add-medical-lien/medical-codes-description";
import UploadDocuments from "./forms/add-medical-lien/medical-upload-document";
import { LienInfoParams } from "@/lib/liens/liens.types";
import { liensService } from "@/lib/selling";
import { parsePricingRow } from "@/lib/selling/selling-detail.mapper";
import Link from "next/link";
import { useToast } from "@/lib/toast-context";
import { useRouter } from "next/navigation";
export interface AddLienComponentProps {
  // Existing draft lien to resume (from the /selling/add-liens/[lienId] route).
  // Omitted when starting a brand-new lien from /selling/add-liens.
  lienId?: string;
  caseId?: string;
  caseInfo?: any;
  purchase?: any;
  onClose?: () => void;
}

const steps = [
  { number: 1, label: "Medical Lien Information", icon: "ri-hospital-line" },
  { number: 2, label: "Facility / Provider", icon: "ri-stethoscope-line" },
  { number: 3, label: "Codes / Description", icon: "ri-capsule-line" },
  { number: 4, label: "Upload Docs", icon: "ri-upload-line" },
];

function ProgressBar({ currentStep }: { currentStep: number }) {
  const progressPercent = ((currentStep - 1) / (steps.length - 1)) * 100;
  const totalSteps = steps.length;
  // circle size used for padding so bar aligns with circle centers
  const circleSize = 50;

  return (
    <div className="w-full mx-auto p-4">
      {/* Segmented Progress Track */}
      <div className="flex w-full gap-2">
        {Array.from({ length: totalSteps }, (_, index) => {
          // A segment is filled if its index is less than the current step
          const isFilled = index < currentStep;

          return (
            <div
              key={index}
              className={`h-1 flex-1 rounded-full transition-all duration-300 ease-in-out ${
                isFilled ? "bg-[#EE7132]" : "bg-gray-200"
              }`}
            />
          );
        })}
      </div>
    </div>
  );
}

export default function AddLienComponent(props: AddLienComponentProps) {
  const { show: showToast } = useToast();
  const router = useRouter();
  const [errors, setErrors] = useState<Record<string, string>>({});

  const { caseId, caseInfo, purchase, onClose } = props;
  const totalSteps = steps.length;
  const [currentStep, setCurrentStep] = useState<number>(1);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [hydrating, setHydrating] = useState(!!props.lienId);

  const [forms, setForms] = useState<any[]>(Array(totalSteps).fill(null));
  // Step 4 (upload docs) is optional — there's nothing to invalidate, so it
  // starts valid instead of waiting for a signal from the child form.
  const [valid, setValid] = useState<Record<number, boolean>>({
    [totalSteps]: true,
  });
  const isLastStep = currentStep === steps.length;
  const [liensId, setLiensId] = useState(props.lienId ?? "");
  const notComplete = useMemo(() => !valid[currentStep], [valid, currentStep]);

  // Resuming an existing draft (came in via /selling/add-liens/[lienId], either
  // from the redirect below or a page refresh) — hydrate the wizard's forms
  // from the lien instead of starting a second bare lien from scratch.
  useEffect(() => {
    if (!props.lienId) return;
    let cancelled = false;
    (async () => {
      try {
        const lien = await liensService.getLienById(props.lienId!);
        if (cancelled) return;

        const rows = lien.medicalPricing.rows.map((row) => {
          const parsed = parsePricingRow(row);
          return {
            id: row.id,
            code: parsed.medicalCode,
            description: parsed.description ?? "",
            billingAmount: parsed.billingAmount,
            medicareCost: parsed.medicareCost,
            targetSaleAmount: parsed.targetSaleAmount,
          };
        });

        setForms([
          {
            status: lien.lienInformation.sellerStatus,
            listingVisibility: lien.lienInformation.listingVisibility,
            initialServiceDate: lien.lienInformation.initialServiceDate ?? "",
            endServiceDate: lien.lienInformation.endServiceDate ?? "",
            notes: lien.lienInformation.notes ?? "",
          },
          {
            fundingCompanyId: lien.fundingCompany?.id ?? "",
            fundingCompany: lien.fundingCompany?.name ?? "",
            facilityContactId: lien.fundingCompany?.contact?.id ?? "",
            facilityContact: lien.fundingCompany?.contact?.name ?? "",
            lawfirmId: lien.caseInformation?.lawFirmId ?? "",
            caseManagerId: lien.caseInformation?.caseManagerId ?? "",
          },
          { codeRows: rows },
          null,
        ]);

        const resumeStep =
          rows.length > 0
            ? 4
            : lien.caseInformation?.lawFirmId || lien.fundingCompany
              ? 3
              : 2;
        setCurrentStep(resumeStep);
      } catch (err) {
        showToast(
          err instanceof Error ? err.message : "Failed to load lien",
          "error",
        );
      } finally {
        if (!cancelled) setHydrating(false);
      }
    })();
    return () => {
      cancelled = true;
    };
    // Mount-only: props.lienId is fixed for the lifetime of this component
    // instance (a route param change remounts it).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function closeModal() {
    onClose?.();
  }

  function startLoading() {
    setSubmitting(true);
  }
  function stopLoading() {
    setSubmitting(false);
  }

  const handleBackOrCancel = () => {
    if (currentStep > 1) {
      setCurrentStep((s) => s - 1);
    } else {
      router.back();
    }
  };

  const handleNextOrSubmit = async () => {
    if (currentStep == 1) {
      return await createLienInfo(forms[0]);
    }
    if (currentStep == 2) {
      return await saveCaseInfo(forms[1]);
    }
    if (currentStep == 3) {
      return await saveMedicalPricingInfo(forms[2]);
    }
    if (currentStep == 4) {
      // Documents are attached (and persisted via saveDocuments) as soon as
      // each file uploads in the UploadDocuments step, so there's nothing
      // left to save here — just confirm and return to the portfolio.
      showToast("Lien added successfully.", "success");
      router.push("/selling/portfolio");
      return;
    }
    if (!isLastStep) {
      setCurrentStep((s) => s + 1);
      return;
    }
  };

  const createLienInfo = async (payload: any) => {
    startLoading();
    try {
      const request: LienInfoParams = {
        sellerStatus: payload.status,
        initialServiceDate: payload.initialServiceDate,
        endServiceDate: payload.endServiceDate || null,
        listingVisibility: payload.listingVisibility,
        notes: payload.notes,
      };

      // The lien only gets created once. Re-submitting step 1 after going
      // back to it (or resuming a draft from /selling/add-liens/[lienId])
      // must update that same lien's info, not POST /liens again.
      if (liensId) {
        await liensService.createLienInfo(liensId, request);
        setErrors({});
        setCurrentStep((s) => s + 1);
        return;
      }

      // There's no "create draft" endpoint — a lien only gets an id via
      // POST /liens (liensService.createLien), then the rest of step 1's
      // fields are saved with a separate PUT .../lien-information call.
      // See the comment on liensApi.createLien for the backend source.
      const created = await liensService.createLien({
        sellerStatus: request.sellerStatus,
        source: "Single",
      });
      await liensService.createLienInfo(created.lienId, request);
      setLiensId(created.lienId);
      showToast("Liens Created", "success");
      setErrors({});
      setCurrentStep((s) => s + 1);
      // Move the URL onto the resumable draft route so a refresh (or the
      // back button) continues editing this lien instead of creating another
      // bare one. The backend has no draft-listing endpoint yet, so this URL
      // is the only way progress survives a refresh.
      router.replace(`/selling/add-liens/${created.lienId}`);
    } catch (err) {
      if (err instanceof ApiError) {
        console.log(err.message);
        showToast(err.message, "error");
      } else {
        showToast("An unexpected error occurred", "error");
      }
    } finally {
      stopLoading();
    }
  };

  const saveCaseInfo = async (payload: any) => {
    startLoading();
    try {
      await liensService.saveCaseInformation(liensId, {
        fundingCompanyId: payload?.fundingCompanyId || undefined,
        fundingCompanyContactId: payload?.facilityContactId || undefined,
        handlingLawFirmId: payload?.lawfirmId || undefined,
        caseManagerId: payload?.caseManagerId || undefined,
        caseId: caseId || undefined,
        createCaseIfMissing: !caseId,
      });
      setErrors({});
      setCurrentStep((s) => s + 1);
    } catch (err) {
      if (err instanceof ApiError) {
        showToast(err.message, "error");
      } else {
        showToast("An unexpected error occurred", "error");
      }
    } finally {
      stopLoading();
    }
  };

  const saveMedicalPricingInfo = async (payload: any) => {
    startLoading();
    try {
      const rows = payload?.codeRows ?? [];
      const askAmount = rows.reduce(
        (sum: number, row: any) => sum + (row.targetSaleAmount || 0),
        0,
      );
      const totalBillingAmount = rows.reduce(
        (sum: number, row: any) => sum + (row.billingAmount || 0),
        0,
      );

      await liensService.saveMedicalPricing(liensId, {
        askAmount,
        billingAmount: totalBillingAmount,
        rows: rows.map((row: any) => ({
          medicalCode: row.code,
          description: row.description || undefined,
          billingAmount: row.billingAmount,
          medicareCost: row.medicareCost,
          targetSaleAmount: row.targetSaleAmount,
        })),
      });
      setErrors({});
      setCurrentStep((s) => s + 1);
    } catch (err) {
      if (err instanceof ApiError) {
        showToast(err.message, "error");
      } else {
        showToast("An unexpected error occurred", "error");
      }
    } finally {
      stopLoading();
    }
  };

  function onFormValid(isValid: boolean, data?: any) {
    setValid((s) => ({ ...s, [currentStep]: !!isValid }));
    setForms((arr) => {
      const copy = [...arr];
      copy[currentStep - 1] = data ?? copy[currentStep - 1];
      return copy;
    });
  }

  return (
    <div className="max-w-[700px] m-auto">
      <div className="flex items-center mb-6 ">
        <nav>
          <Link
            href="/selling/portfolio"
            className="text-sm text-gray-500 hover:text-gray-800"
          >
            <i className="ri-arrow-left-line text-xl" />
          </Link>
        </nav>
        <ProgressBar currentStep={currentStep} />
      </div>
      <p className={`mt-2 text-xs text-gray-600`}>
        Step {currentStep}/ {steps.length}
      </p>
      <div className="mt-5 position-relative ">
        {loading && (
          <div
            className="loading-overlay d-flex justify-content-center align-items-center"
            style={{
              position: "absolute",
              inset: 0,
              background: "rgba(255,255,255,0.6)",
            }}
          >
            <div className="spinner-border m-2" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        )}

        {hydrating && (
          <div className="py-16 text-center">
            <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          </div>
        )}

        {!hydrating && currentStep === 1 && (
          <LienInfo
            caseId={caseId}
            lienId={liensId}
            data={forms[0]}
            onFormValid={onFormValid}
          />
        )}
        {!hydrating && currentStep === 2 && (
          <FundingCompanyInfo
            caseId={caseId}
            lienId={liensId}
            data={forms[1]}
            onFormValid={onFormValid}
          />
        )}
        {!hydrating && currentStep === 3 && liensId && (
          <MedicalCodesDescription
            caseId={caseId}
            lienId={liensId}
            data={forms[2]}
            onFormValid={onFormValid}
          />
        )}
        {!hydrating && currentStep === 4 && liensId && (
          <UploadDocuments
            data={forms[3]}
            caseId={caseId}
            lienId={liensId}
            onUploaded={onFormValid}
          />
        )}

        <div className="px-6 py-3 border-t border-gray-100 flex items-center justify-end gap-2">
          {/* LEFT BUTTON */}
          <button
            onClick={handleBackOrCancel}
            className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
          >
            {currentStep === 0 ? "Cancel" : "Back"}
          </button>

          {/* RIGHT BUTTON */}
          <button
            onClick={handleNextOrSubmit}
            className="text-sm px-4 py-2 bg-[#EE7132] text-white rounded-lg hover:bg-[#EE7132]/90 disabled:bg-[#EE7132]/70"
            disabled={notComplete || submitting}
          >
            {isLastStep ? "Add Lien" : "Continue"}
          </button>
        </div>
      </div>
    </div>
  );
}
