"use client";

import { useCallback, useEffect, useState } from "react";
import { useLienStore } from "@/stores/lien-store";
import { casesService } from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import type {
  CreateMedicalCodeLiensDto,
  CreateMedicalFacilityDto,
  CreateMedicalLiensDto,
  CreateMedicalPaymentDto,
} from "@/lib/cases/cases.types";
import { MedicalLienDetailSection } from "./sections/medical-lien-detail-section";

export function LienDetailView({
  caseId,
  lienId,
  onGoBack,
}: {
  caseId: string;
  lienId: string;
  onGoBack: () => void;
}) {
  const addToast = useLienStore((s) => s.addToast);
  const [loading, setLoading] = useState(true);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [forms, setForms] = useState<Record<number, any>>({
    [0]: undefined,
    [1]: undefined,
    [2]: undefined,
  });
  const [data, setData] = useState<Record<number, any>>({
    [0]: undefined,
    [1]: undefined,
    [2]: undefined,
  });

  const fetchLienDetails = useCallback(async () => {
    try {
      setLoading(true);
      const taskPromises = [
        casesService.getMedicalInfo(lienId),
        casesService.getMedicalFacility(lienId),
        casesService.getMedicalCodes(lienId),
        casesService.loadLiensDocuments(lienId),
        casesService.getPayee(lienId),
      ];

      const results = await Promise.allSettled(taskPromises);

      results.forEach((result, index) => {
        if (result.status === "fulfilled") {
          if (result.value.data) {
            setData((prev) => ({
              ...prev,
              [index]: { ...result.value.data, hasInitialValue: true },
            }));
          }
        } else {
          console.error(`Task ${index} failed due to:`, result.reason);
        }
      });
    } catch (error) {
      console.error("Unexpected execution error", error);
    } finally {
      setLoading(false);
    }
  }, [lienId]);

  const fetchLienDocuments = useCallback(async () => {
    try {
      const docs = await casesService.loadLiensDocuments(lienId);
      setData((prev) => ({
        ...prev,
        [3]: docs.data,
      }));
    } catch (error) {
      console.error("Unexpected execution error", error);
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [forms[3], lienId]);

  useEffect(() => {
    fetchLienDetails();
  }, [fetchLienDetails]);

  function onFormValid(formData: any, index: number) {
    setForms((prev: Record<number, any>) => {
      const copy = prev;
      copy[index] = formData ?? copy[index];
      return copy;
    });
  }

  const dateConverter = (dateData: string) => {
    if (!dateData) return;

    const date = new Date(dateData);
    const formatter = new Intl.DateTimeFormat("en-US", {
      month: "2-digit",
      day: "2-digit",
      year: "numeric",
    });

    return formatter.format(date);
  };

  const saveMedicalLien = async (payload: CreateMedicalLiensDto) => {
    try {
      const request: CreateMedicalLiensDto = {
        id: forms[0].id,
        caseId: caseId,
        status: payload.status,
        purchaseDate: payload.purchaseDate,
        initialServiceDate: payload.initialServiceDate,
        endServiceDate: payload.endServiceDate,
        note: payload.note,
        isBulk: payload.isBulk == "true" ? "Yes" : "No",
        isServicing: payload.isServicing == "true" ? "Yes" : "No",
        fundingCompanyId: payload.fundingCompanyId,
      };
      !forms[0].hasInitialValue
        ? await casesService.createMedicalLiens(request)
        : await casesService.updateMedicalLiens(request);

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
          title: "Update Medical Information Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const saveMedicalFacilityLiens = async (
    payload: CreateMedicalFacilityDto,
  ) => {
    if (!payload.facilityId) return;
    try {
      const request: CreateMedicalFacilityDto = {
        liensId: lienId,
        facilityId: payload.facilityId,
        facility: payload.facility,
        facilityContactId: payload.facilityContactId,
        facilityContact: payload.facilityContact,
        email: payload.email,
        medicalProviderId: payload.medicalProviderId,
        medicalProvider: payload.medicalProvider,
      };
      !forms[1].hasInitialValue
        ? await casesService.createMedicalFacilityLiens(request)
        : await casesService.updateMedicalFacilityLiens(request);
      addToast({
        type: "success",
        title: "Facility Updated",
        description: `Facility has been updated.`,
      });
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Update Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Update Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const saveMedicalPayee = async (payload: CreateMedicalPaymentDto) => {
    try {
      const request: CreateMedicalPaymentDto = {
        id: null,
        liensId: lienId,
        payee: payload.payee,
        outboundCheckNumber: payload.outboundCheckNumber,
      };
      await casesService.createMedicalPaymentLiens(request);
      addToast({
        type: "success",
        title: "Payee Updated",
        description: `Payee has been updated.`,
      });
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Update Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Update Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const updateMedicalCodeLiens = async (payload: CreateMedicalCodeLiensDto) => {
    try {
      const request: CreateMedicalCodeLiensDto = {
        id: payload?.id?.includes("temp") ? null : payload.id,
        liensId: lienId,
        code: payload.code,
        medicareCost: parseFloat(payload.medicareCost).toFixed(2),
        billingAmount: parseFloat(payload.billingAmount).toFixed(2),
        purchaseAmount: parseFloat(payload.purchaseAmount).toFixed(2),
        payee: payload.payee,
        outboundCheckNumber: payload.outboundCheckNumber,
      };
      request.id == null
        ? await casesService.createMedicalCodeLiens(request)
        : await casesService.updateMedicalCodeLiens(request);
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Update Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Update Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  async function save() {
    Promise.allSettled([
      await saveMedicalLien({
        ...forms[0],
        purchaseDate: dateConverter(forms[0].purchaseDate),
        initialServiceDate: dateConverter(forms[0].initialServiceDate),
        endServiceDate: dateConverter(forms[0].endServiceDate),
      }),
      await saveMedicalFacilityLiens(forms[1]),

      forms[2]?.codeRows?.forEach(async (element: any) => {
        await updateMedicalCodeLiens({
          payee: forms[2].payee,
          outboundCheckNumber: forms[2].outboundCheckNumber,
          ...element,
        });
      }),
      await saveMedicalPayee(forms[2]),
    ]);

    addToast({
      type: "success",
      title: "Liens Updated",
      description: `Liens has been updated.`,
    });
    onGoBack();
  }

  return (
    <MedicalLienDetailSection
      caseId={caseId}
      lienId={lienId}
      loading={loading}
      data={data}
      onFormValid={onFormValid}
      onDocumentsUploaded={fetchLienDocuments}
      onGoBack={onGoBack}
      onSave={() => save()}
    />
  );
}
