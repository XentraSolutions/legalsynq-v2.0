import { useEffect, useState, type RefObject } from "react";
import Field from "@/components/lien/field";
import UploadDocumentComponent, {
  FileDropzoneRef,
} from "@/components/lien/upload-document";
import type { DropdownOption } from "@/lib/lookup/lookup.types";
import { CollapsibleSection } from "../../../components/collapsible-section";

export function UploadDocumentSection({
  submitting,
  docTypes,
  selectedDocType,
  onSelectedDocTypeChange,
  selectedFiles,
  onFilesChange,
  dropzoneRef,
  onAddDocument,
}: {
  submitting: boolean;
  docTypes: DropdownOption[];
  selectedDocType: string;
  onSelectedDocTypeChange: (v: string) => void;
  selectedFiles: File[] | null;
  onFilesChange: (files: File[]) => void;
  dropzoneRef: RefObject<FileDropzoneRef>;
  onAddDocument: () => void;
}) {
  const [isSubmitting, setIsSubmitting] = useState<boolean>(submitting);
  useEffect(() => {
    setIsSubmitting(submitting);
  }, [submitting]);
  return (
    <CollapsibleSection title="Upload Document" icon="ri-upload-cloud-2-line">
      <div className="space-y-4">
        <div>
          <label className="block text-xs font-medium text-gray-500 uppercase tracking-wide mb-1.5">
            Document Type
          </label>
          <div className="relative">
            <Field
              label=""
              value={selectedDocType}
              options={docTypes}
              onChange={(v: string) => onSelectedDocTypeChange(v.toString())}
              placeholder="Select document type..."
              type="select"
            />
          </div>
        </div>

        <UploadDocumentComponent
          ref={dropzoneRef}
          onUploaded={(e) => onFilesChange(e)}
        />

        <button
          disabled={isSubmitting || !selectedFiles?.length || !selectedDocType}
          className="w-full px-4 py-2.5 text-sm font-medium rounded-lg bg-primary text-white hover:bg-primary/90 disabled:bg-gray-100 disabled:text-gray-400 disabled:cursor-not-allowed"
          onClick={(e) => {
            e.stopPropagation();
            setIsSubmitting(true);
            onAddDocument();
          }}
        >
          <i className="ri-add-line text-sm" />
          Add Document
        </button>
      </div>
    </CollapsibleSection>
  );
}
