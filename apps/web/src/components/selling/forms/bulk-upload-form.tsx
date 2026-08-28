"use client";

import { useState, useRef } from "react";
import { CloudUpload, Download, FileText, X } from "lucide-react";
import { toast } from "sonner";
import { FormModal } from "@/components/selling/modal";
import { useLienStore } from "@/stores/lien-store";
import { documentsService } from "@/lib/documents";
import { liensService } from "@/lib/selling";
import { liensApi } from "@/lib/selling/selling-liens.api";
import { stripFirstCsvColumn, prependCsvColumn } from "@/lib/selling/csv-utils";
import { ApiError } from "@/lib/api-client";
import { Button } from "@/components/selling/button";
import { ReviewBulkUploadModal } from "@/components/selling/forms/review-bulk-upload-modal";

interface BulkUploadFormProps {
  open: boolean;
  onClose: () => void;
  onUploaded?: () => void;
  referenceType?: string;
  referenceId?: string;
  tenantId?: string;
  documentTypeId?: string;
  // When set, the form is scoped to a single case: the template's first
  // column ("Case Code*") is stripped on download since the case is already
  // known, and re-added with this value before the file is uploaded.
  caseCode?: string;
}

// Matches the header SellingBulkImportTemplateColumns puts first — see the
// comment on BulkImportRowItem in liens.types.ts.
const CASE_CODE_HEADER = "Case Code*";

const REFERENCE_TYPE_OPTIONS = ["Case", "Lien", "Bill of Sale"];

const DEFAULT_TENANT_ID = "00000000-0000-0000-0000-000000000001";
const DEFAULT_DOC_TYPE_ID = "00000000-0000-0000-0000-000000000001";

export function BulkUploadForm({
  open,
  onClose,
  onUploaded,
  referenceType,
  referenceId,
  tenantId,
  documentTypeId,
  caseCode,
}: BulkUploadFormProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [form, setForm] = useState({
    title: "",
    description: "",
    referenceType: referenceType || "",
    referenceId: referenceId || "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [dragOver, setDragOver] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [reviewImportId, setReviewImportId] = useState<string | null>(null);
  const [confirming, setConfirming] = useState(false);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!file) e.file = "File is required";
    if (!form.title.trim()) e.title = "Title is required";
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate() || !file) return;
    try {
      setUploading(true);
      const formData = new FormData();
      if (!file) return;
      const uploadFile = caseCode
        ? new File(
            [prependCsvColumn(await file.text(), CASE_CODE_HEADER, caseCode)],
            file.name,
            { type: file.type || "text/csv" },
          )
        : file;
      formData.append("File", uploadFile);
      formData.append("templateType", "SellingLienImport");
      formData.append("defaultListingVisibility", "Private");
      formData.append("defaultSellerStatus", "Pending");
      const uploadedfiles = await liensService.upload(formData);
      const validated = await liensService.validateUpload(
        uploadedfiles.importId,
      );

      if (validated.status == "VALIDATED_WITH_ERRORS") {
        toast.error(`Failed to import ${validated.invalidCount} row/s`);
        return;
      }

      setReviewImportId(uploadedfiles.importId);
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err?.message ?? "Unexpected error");
      }
    } finally {
      setUploading(false);
    }
  };

  const handleFileSelect = (files: FileList | null) => {
    if (files && files.length > 0) {
      const selected = files[0];
      setFile(selected);
      if (!form.title.trim()) {
        setForm((f) => ({ ...f, title: selected.name }));
      }
    }
  };

  const resetAndClose = () => {
    setFile(null);
    setErrors({});
    setReviewImportId(null);
    onClose();
  };

  const handleConfirmImport = async () => {
    if (!reviewImportId) return;
    try {
      setConfirming(true);
      const confirmed = await liensService.confirmUpload(reviewImportId);
      if (confirmed.status == "CONFIRMED" || confirmed.status == "PARTIAL") {
        toast.success(
          `${confirmed.createdCount} ${confirmed.createdCount > 1 ? "liens" : "lien"} has been successfully added to the Portfolio.`,
        );
        onUploaded?.();
        resetAndClose();
      }
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err?.message ?? "Unexpected error");
      }
    } finally {
      setConfirming(false);
    }
  };

  const downloadTemplate = async () => {
    let blob = await liensService.downloadTemplate();
    if (caseCode) {
      blob = new Blob([stripFirstCsvColumn(await blob.text())], {
        type: blob.type || "text/csv",
      });
    }
    const url = URL.createObjectURL(blob);

    const link = document.createElement("a");
    link.href = url;
    link.download = "template.csv";
    document.body.appendChild(link);
    link.click();

    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <>
    <FormModal
      open={open && !reviewImportId}
      onClose={resetAndClose}
      onSubmit={handleSubmit}
      title=""
      hasHeader={false}
      submitLabel={uploading ? "Uploading..." : "Continue"}
    >
      <div className="space-y-4">
        <input
          ref={fileInputRef}
          type="file"
          className="hidden"
          accept={caseCode ? ".csv" : ".csv,.xlsx"}
          onChange={(e) => handleFileSelect(e.target.files)}
        />
        {file && (
          <div>
            <label className="block text-xl font-medium text-gray-700 mb-1">
              Lien Bulk Upload
            </label>
            <p className="text-sm text-gray-600 mb-1">
              Import multiple liens in a single upload by selecting a
              supported file.
            </p>
          </div>
        )}
        <div
          className={`border-2 border relative rounded-xl p-8 text-center transition-colors cursor-pointer overflow-hidden border-gray-200 ${dragOver ? "border-primary bg-primary/5" : "border-gray-200"}`}
          onClick={() => fileInputRef.current?.click()}
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragOver(false);
            handleFileSelect(e.dataTransfer.files);
          }}
        >
          {file ? (
            <div className="flex items-center w-full gap-2">
              <FileText className="h-6 w-6" />
              <div className="flex-1 min-w-0  text-left">
                <span className="block text-sm font-medium text-gray-700 truncate">
                  {file.name}
                </span>
                <p className="text-xs text-gray-400">
                  {(file.size / 1024).toFixed(0)} KB
                </p>
              </div>
              <Button
                type="button"
                variant="ghost"
                className="w-7 h-7 p-0 text-gray-400 hover:text-gray-600"
                onClick={(e) => {
                  e.stopPropagation();
                  setFile(null);
                }}
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          ) : (
            <>
              <CloudUpload className="h-6 w-6 p-2 px-3 rounded-lg bg-[#F5F5F5] box-content mx-auto" />
              <div>
                <label className="block text-xl font-medium text-gray-700 mb-1 mt-4">
                  Lien Bulk Upload
                </label>
                <p className="text-sm text-gray-600 mb-1">
                  Import multiple liens in a single upload by selecting a
                  supported file.
                </p>
                <Button
                  type="button"
                  variant="secondary"
                  className="border-gray-400 mt-2"
                  onClick={() => fileInputRef.current?.click()}
                >
                  Choose File
                </Button>
              </div>
            </>
          )}
        </div>
        <button
          onClick={() => {
            downloadTemplate();
          }}
          className="inline-flex items-center gap-1.5 text-xs cursor-pointer text-[#EE7132]"
        >
          <Download className="h-3.5 w-3.5" />
          Download Template
        </button>
        {errors.file && <p className="text-xs text-red-500">{errors.file}</p>}
      </div>
    </FormModal>
    <ReviewBulkUploadModal
      open={!!reviewImportId}
      importId={reviewImportId}
      onClose={resetAndClose}
      onConfirm={handleConfirmImport}
      confirming={confirming}
    />
    </>
  );
}
