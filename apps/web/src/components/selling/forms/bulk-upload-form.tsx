"use client";

import { useState, useRef } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { documentsService } from "@/lib/documents";
import { liensService } from "@/lib/selling";
import { liensApi } from "@/lib/selling/selling-liens.api";
import { useToast } from "@/lib/toast-context";
import { ApiError } from "@/lib/api-client";

interface BulkUploadFormProps {
  open: boolean;
  onClose: () => void;
  onUploaded?: () => void;
  referenceType?: string;
  referenceId?: string;
  tenantId?: string;
  documentTypeId?: string;
}

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
}: BulkUploadFormProps) {
  const { show: showToast } = useToast();

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
      formData.append("File", file);
      formData.append("templateType", "SellingLienImport");
      formData.append("defaultListingVisibility", "Private");
      formData.append("defaultSellerStatus", "Pending");
      const uploadedfiles = await liensService.upload(formData);
      console.log(uploadedfiles);
      const validated = await liensService.validateUpload(
        uploadedfiles.importId,
      );
      const confirmed = await liensService.confirmUpload(
        uploadedfiles.importId,
      );
      console.log(confirmed);
      if (confirmed.status == "CONFIRMED") {
        showToast(
          "Lien has been successfully added to the Portfolio.",
          "success",
        );
      }
      resetAndClose();
    } catch (err) {
      if (err instanceof ApiError) {
        showToast(err?.message ?? "Unexpected error", "error");
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
    onClose();
  };

  const downloadTemplate = async () => {
    const csv = await liensService.downloadTemplate();
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
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
    <FormModal
      open={open}
      onClose={resetAndClose}
      onSubmit={handleSubmit}
      title="Upload Document"
      submitLabel={uploading ? "Uploading..." : "Upload"}
    >
      <div className="space-y-4">
        <input
          ref={fileInputRef}
          type="file"
          className="hidden"
          accept=".csv,.xlsx"
          onChange={(e) => handleFileSelect(e.target.files)}
        />
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
              <i className="ri-file-text-line text-2xl" />
              <div className="flex-1 min-w-0  text-left">
                <span className="block text-sm font-medium text-gray-700 truncate">
                  {file.name}
                </span>
                <p className="text-xs text-gray-400">
                  {(file.size / 1024).toFixed(0)} KB
                </p>
              </div>
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  setFile(null);
                }}
                className="text-gray-400 hover:text-gray-600"
              >
                <i className="ri-close-line" />
              </button>
            </div>
          ) : (
            <>
              <i className="ri-upload-cloud-2-line text-3xl p-2 px-3 rounded-lg bg-[#F5F5F5]" />
              <div>
                <label className="block text-xl font-medium text-gray-700 mb-1 mt-4">
                  Lien Bulk Upload
                </label>
                <p className="text-sm text-gray-600 mb-1">
                  Import multiple liens in a single upload by selecting a
                  supported file. Review your data to ensure all required
                  information is included.
                </p>
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  className="rounded-lg border-gray-400 border-1 p-2 mt-2 hover:text-gray-600"
                >
                  Choose File
                </button>
              </div>
            </>
          )}
        </div>
        <button
          onClick={() => {
            downloadTemplate();
          }}
        >
          download template
        </button>
        {errors.file && <p className="text-xs text-red-500">{errors.file}</p>}
      </div>
    </FormModal>
  );
}
