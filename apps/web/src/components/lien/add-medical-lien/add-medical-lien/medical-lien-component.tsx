import React, { useEffect, useMemo, useState } from "react";
import MedicalLienInfo from "../../forms/add-medical-lien/medical-lien-info";
import { Modal } from "@/components/lien/modal";

import "./medical-lien-component.css";
import {
  CreateMedicalCodeLiensDto,
  CreateMedicalFacilityDto,
  CreateMedicalLiensDto,
} from "@/lib/cases/cases.types";
import { casesService } from "@/lib/cases";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import MedicalFacilityProviderInfo from "../../forms/add-medical-lien/medical-facility-provider-info";
import MedicalCodesDescription from "../../forms/add-medical-lien/medical-codes-description";
import UploadDocuments from "../../forms/add-medical-lien/medical-upload-document";
export interface MedicalLienComponentProps {
  caseId: string;
  lienId?: string;
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

  // circle size used for padding so bar aligns with circle centers
  const circleSize = 50;

  return (
    <div className="w-full mb-3 px-2">
      <div className="relative w-full">
        <div className="relative flex items-start">
          {/* Track */}
          <div
            className="absolute h-0.5 rounded-full"
            style={{
              width: `calc(100% - ${circleSize * 2}px)`,
              top: `${circleSize / 2}px`,
              left: `${circleSize}px`,
              right: `calc(${circleSize}px / 2)`,
              transform: "translateY(-50%)",
              backgroundColor: "#7E7E7E",
            }}
          />

          {/* Progress */}
          <div
            className="absolute h-0.5 rounded-full"
            style={{
              top: `${circleSize / 2}px`,
              left: `${circleSize}px`,
              width: `calc((100% - ${circleSize * 2}px) * ${progressPercent / 100})`,
              transform: "translateY(-50%)",
              backgroundColor: "#0D2A4A",
            }}
          />

          {steps.map((step, idx) => {
            const done = currentStep > step.number;
            const active = currentStep === step.number;
            return (
              <div key={step.number} className="flex flex-1 justify-center">
                <div className="flex flex-col items-center">
                  <div
                    className={`flex items-center justify-center rounded-full text-sm font-semibold transition-colors z-10 ${done ? "bg-[#184C87] text-white" : active ? "bg-white border-2 border-primary text-primary" : "bg-white border border-gray-200 text-gray-600"}`}
                    style={{ width: circleSize, height: circleSize }}
                    aria-current={active ? "step" : undefined}
                  >
                    <i className={step.icon} />
                  </div>
                  <small
                    className={`mt-2 text-xs text-center ${done ? "text-[#184C87] font-semibold" : active ? "text-[#184C87] font-semibold" : "text-gray-600"}`}
                  >
                    {step.label}
                  </small>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

export default function MedicalLienComponent(props: MedicalLienComponentProps) {
  const addToast = useLienStore((s) => s.addToast);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const { caseId, lienId, caseInfo, purchase, onClose } = props;
  const totalSteps = steps.length;
  const [currentStep, setCurrentStep] = useState<number>(1);
  const [loading, setLoading] = useState(false);
  const [forms, setForms] = useState<any[]>(Array(totalSteps).fill(null));
  const [valid, setValid] = useState<Record<number, boolean>>({});
  const isLastStep = currentStep === steps.length;

  const notComplete = useMemo(() => !valid[currentStep], [valid, currentStep]);

  function closeModal() {
    onClose?.();
  }

  function startLoading() {
    setLoading(true);
  }
  function stopLoading() {
    setLoading(false);
  }

  const handleBackOrCancel = () => {
    if (currentStep > 0) {
      setCurrentStep((s) => s - 1);
    } else {
      onClose();
    }
  };

  const handleNextOrSubmit = async () => {
    console.log(forms, currentStep);
    if (currentStep == 1) {
      createMedicalLien(forms[0]);
    }

    if (currentStep == 4) {
      save();
    }
    if (!isLastStep) {
      setCurrentStep((s) => s + 1);
      return;
    }
  };

  const createMedicalLien = async (payload: CreateMedicalLiensDto) => {
    console.log(payload);
    try {
      const request: CreateMedicalLiensDto = {
        id: null,
        caseId: caseId,
        status: payload.status,
        purchaseDate: payload.purchaseDate,
        initialServiceDate: payload.initialServiceDate,
        endServiceDate: payload.endServiceDate,
        note: payload.note,
        isBulk: payload.isBulk == "true" ? "Y" : "N",
        isServicing: payload.isBulk == "true" ? "Y" : "N",
        fundingCompanyId: payload.fundingCompanyId,
      };
      await casesService.createMedicalLiens(request);
      addToast({
        type: "success",
        title: "Liens Created",
        description: `Liens has been created.`,
      });
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isConflict) {
          setErrors({ caseNumber: "A case with this number already exists" });
        } else {
          addToast({
            type: "error",
            title: "Create Failed",
            description: err.message,
          });
        }
      } else {
        addToast({
          type: "error",
          title: "Create Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const createMedicalFacilityLiens = async (
    payload: CreateMedicalFacilityDto,
  ) => {
    console.log(payload);
    try {
      const request: CreateMedicalFacilityDto = {
        liensId: payload.liensId,
        facilityId: payload.facilityId,
        facility: payload.facility,
        facilityContactId: payload.facilityContactId,
        facilityContact: payload.facilityContact,
        email: payload.email,
        medicalProviderId: payload.medicalProviderId,
        medicalProvider: payload.medicalProvider,
      };
      await casesService.createMedicalFacilityLiens(request);
      addToast({
        type: "success",
        title: "Facility Created",
        description: `Facility has been created.`,
      });
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Create Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Create Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const createMedicalCodeLiens = async (payload: CreateMedicalCodeLiensDto) => {
    console.log(payload);
    try {
      const request: CreateMedicalCodeLiensDto = {
        id: payload.id,
        liensId: payload.liensId,
        code: payload.code,
        medicareCost: payload.medicareCost,
        billingAmount: payload.billingAmount,
        purchaseAmount: payload.purchaseAmount,
      };
      await casesService.createMedicalCodeLiens(request);
      addToast({
        type: "success",
        title: "Medical Code Created",
        description: `Medical Code has been created.`,
      });
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Create Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Create Failed",
          description: "An unexpected error occurred",
        });
      }
    }
    // finally {
    //   setSubmitting(false);
    // }
  };

  function onFormValid(isValid: boolean, data?: any) {
    setValid((s) => ({ ...s, [currentStep]: !!isValid }));
    setForms((arr) => {
      const copy = [...arr];
      copy[currentStep - 1] = data ?? copy[currentStep - 1];
      return copy;
    });
  }

  async function save() {
    if (notComplete) return;
    startLoading();
    try {
      // Implement save logic here (API call)
      Promise.allSettled([
        await createMedicalFacilityLiens(forms[1]),
        await createMedicalCodeLiens(forms[2]),
        await createMedicalCodeLiens(forms[3]),
      ]);
      closeModal();
    } finally {
      stopLoading();
    }
  }

  return (
    <div>
      <Modal
        open={true}
        onClose={onClose}
        title="Create Report"
        subtitle="Configure your report step by step"
        size="lg"
        footer={
          <div className="flex justify-between w-full">
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
              className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90"
            >
              {isLastStep ? "Save" : "Next"}
            </button>
          </div>
        }
      >
        <div className="modal-body">
          <div className="px-2 p-5 border-b-1">
            <ProgressBar currentStep={currentStep} />
          </div>

          <div className="mt-5 position-relative">
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
              <MedicalLienInfo
                caseId={caseId}
                lienId={lienId}
                data={forms[0]}
                onFormValid={onFormValid}
              />
            )}
            {currentStep === 2 && (
              <MedicalFacilityProviderInfo
                caseId={caseId}
                lienId={lienId}
                data={forms[1]}
                onFormValid={onFormValid}
              />
            )}
            {currentStep === 3 && (
              <MedicalCodesDescription
                caseId={caseId}
                lienId={lienId}
                data={forms[2]}
                onFormValid={onFormValid}
              />
            )}
            {currentStep === 4 && (
              <UploadDocuments
                data={forms[3]}
                caseId={caseId}
                lienId={lienId}
                onFormValid={onFormValid}
              />
            )}
          </div>
        </div>
      </Modal>
    </div>
  );
}
