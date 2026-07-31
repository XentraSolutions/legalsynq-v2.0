import React, { useEffect, useMemo, useState } from "react";
import { Modal } from "@/components/lien/modal";

// import "./medical-lien-component.css";
import {
  CreateMedicalCodeLiensDto,
  CreateMedicalFacilityDto,
  CreateMedicalLiensDto,
  CreateMedicalPaymentDto,
} from "@/lib/cases/cases.types";
import { casesService } from "@/lib/cases";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import { dateConverter } from "@/lib/cases/cases.mapper";
import MedicalLienInfo from "../lien/forms/add-medical-lien/medical-lien-info";
import LienInfo from "./forms/add-medical-lien/lien-info";
import FundingCompanyInfo from "./forms/add-medical-lien/funding-company-info";
import MedicalCodesDescription from "./forms/add-medical-lien/medical-codes-description";
import UploadDocuments from "./forms/add-medical-lien/medical-upload-document";
import { LienInfoParams } from "@/lib/liens/liens.types";
import { liensService } from "@/lib/selling";
import Link from "next/link";
import { useToast } from "@/lib/toast-context";
import { useRouter } from "next/navigation";
export interface AddLienComponentProps {
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

  const [forms, setForms] = useState<any[]>(Array(totalSteps).fill(null));
  // Step 4 (upload docs) is optional — there's nothing to invalidate, so it
  // starts valid instead of waiting for a signal from the child form.
  const [valid, setValid] = useState<Record<number, boolean>>({
    [totalSteps]: true,
  });
  const isLastStep = currentStep === steps.length;
  const [liensId, setLiensId] = useState("");
  const notComplete = useMemo(() => !valid[currentStep], [valid, currentStep]);

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
    if (currentStep == 4) {
      // save();
    }
    if (!isLastStep) {
      setCurrentStep((s) => s + 1);
      return;
    }
  };

  const fetchDocument = async () => {
    const docs = await casesService.loadLiensDocuments(liensId ?? "");
    setForms((prev) => ({ ...prev, 3: docs.data }));
  };

  const createLienInfo = async (payload: LienInfoParams) => {
    startLoading();
    try {
      const request: LienInfoParams = {
        sellerStatus: payload.sellerStatus,
        initialServiceDate: payload.initialServiceDate,
        endServiceDate: payload.endServiceDate,
        listingVisibility: payload.listingVisibility,
        notes: payload.notes,
      };

      await liensService.createLienInfo("", request);
      showToast("Liens Created", "success");
      setErrors({});
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

  function onFormValid(isValid: boolean, data?: any) {
    setValid((s) => ({ ...s, [currentStep]: !!isValid }));
    setForms((arr) => {
      const copy = [...arr];
      copy[currentStep - 1] = data ?? copy[currentStep - 1];
      return copy;
    });
  }

  useEffect(() => {}, [liensId]);

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

        {currentStep === 1 && (
          <LienInfo
            caseId={caseId}
            lienId={liensId}
            data={forms[0]}
            onFormValid={onFormValid}
          />
        )}
        {currentStep === 2 && (
          <FundingCompanyInfo
            caseId={caseId}
            lienId={liensId}
            data={forms[0]}
            onFormValid={onFormValid}
          />
        )}
        {currentStep === 3 && liensId && (
          <MedicalCodesDescription
            caseId={caseId}
            lienId={liensId}
            data={forms[2]}
            onFormValid={onFormValid}
          />
        )}
        {currentStep === 4 && liensId && (
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
            {isLastStep ? "Save" : "Continue"}
          </button>
        </div>
      </div>
    </div>
  );
}
