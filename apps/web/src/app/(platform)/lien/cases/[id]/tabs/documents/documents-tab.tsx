"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useLienStore } from "@/stores/lien-store";
import { casesService, type CaseDetail } from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import { LayoutSplit, type PanelMode } from "@/components/lien/layout-split";
import type { DropdownOption } from "@/lib/lookup/lookup.types";
import { FileDropzoneRef } from "@/components/lien/upload-document";
import { EmailSection } from "../../components/email-section";
import { SmsSection } from "../../components/sms-section";
import { ContactsSection } from "../../components/contacts-section";
import { UploadDocumentSection } from "./sections/upload-document-section";
import { CaseDocumentsSection } from "./sections/case-documents-section";
import { LienDocumentsSection } from "./sections/lien-documents-section";
import type { DocumentType } from "./types";

export function DocumentsTab({
  docTypes,
  caseDetail,
  panelMode,
  lienid,
  onPanelModeChange,
}: {
  docTypes: DropdownOption[];
  caseDetail: CaseDetail;
  panelMode: PanelMode;
  lienid: string;
  onPanelModeChange: (m: PanelMode) => void;
}) {
  const addToast = useLienStore((s) => s.addToast);
  const dropzoneRef = useRef<FileDropzoneRef>(null);

  const [selectedDocType, setSelectedDocType] = useState("");
  const [selectedFiles, setSelectedFiles] = useState<File[] | null>(null);

  const [caseDocuments, setCaseDocuments] = useState<DocumentType[]>([]);
  const [liensDocuments, setLiensDocuments] = useState<DocumentType[]>([]);

  const uploadCaseDocuments = async (payload: any) => {
    if (!payload || payload.length == 0) return;
    try {
      payload.forEach(async (element: File) => {
        const formData = new FormData();
        formData.append("File", element ?? "");
        formData.append("caseId", caseDetail.id ?? "");
        formData.append("DocName", element.name);
        formData.append("DocDescription", "Legacy Case Document upload");
        formData.append("DocFileTypeId", selectedDocType);

        await casesService.uploadCaseDocuments(formData);
        addToast({
          type: "success",
          title: "Document Uploaded",
          description: `Document has been updated.`,
        });
        setTimeout(() => {
          dropzoneRef?.current?.reset();
          setSelectedDocType("");
          fetchDocuments();
        }, 1000);
      });
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

  const fetchDocuments = async () => {
    const docs = await casesService.loadDocuments(caseDetail.id);
    setCaseDocuments(docs.caseDocuments);
    setLiensDocuments(docs.liensDocuments);
  };

  function download(file: any) {
    window.open(file.url || URL.createObjectURL(file as any), "_blank");
  }

  useEffect(() => {
    fetchDocuments();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const leftContent = (
    <div className="space-y-4">
      <UploadDocumentSection
        docTypes={docTypes}
        selectedDocType={selectedDocType}
        onSelectedDocTypeChange={setSelectedDocType}
        selectedFiles={selectedFiles}
        onFilesChange={setSelectedFiles}
        dropzoneRef={dropzoneRef}
        onAddDocument={() => uploadCaseDocuments(selectedFiles)}
      />

      <CaseDocumentsSection
        caseDocuments={caseDocuments}
        onDownload={download}
      />

      <LienDocumentsSection liensDocuments={liensDocuments} />
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <EmailSection />
      <SmsSection />
      <ContactsSection
        items={[
          {
            icon: "ri-building-line",
            iconBgClass: "bg-blue-50",
            iconColorClass: "text-blue-500",
            name: caseDetail.insuranceCarrier || "",
            role: "Law Firm",
          },
        ]}
      />
    </div>
  );

  return (
    <LayoutSplit
      left={leftContent}
      right={rightContent}
      mode={panelMode}
      onModeChange={onPanelModeChange}
    />
  );
}
