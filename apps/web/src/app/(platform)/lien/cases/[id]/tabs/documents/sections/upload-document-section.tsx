import type { RefObject } from "react";
import Field from "@/components/lien/field";
import UploadDocumentComponent, {
  FileDropzoneRef,
} from "@/components/lien/upload-document";
import type { DropdownOption } from "@/lib/lookup/lookup.types";
import { CollapsibleSection } from "../../../components/collapsible-section";

export function UploadDocumentSection({
  docTypes,
  selectedDocType,
  onSelectedDocTypeChange,
  selectedFiles,
  onFilesChange,
  dropzoneRef,
  onAddDocument,
}: {
  docTypes: DropdownOption[];
  selectedDocType: string;
  onSelectedDocTypeChange: (v: string) => void;
  selectedFiles: File[] | null;
  onFilesChange: (files: File[] | null) => void;
  dropzoneRef: RefObject<FileDropzoneRef>;
  onAddDocument: () => void;
}) {
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
          disabled={selectedFiles != null && !selectedDocType}
          className={[
            "w-full px-4 py-2.5 text-sm font-medium rounded-lg transition-colors flex items-center justify-center gap-2",
            selectedFiles && selectedDocType
              ? "bg-primary text-white hover:bg-primary/90"
              : "bg-gray-100 text-gray-400 cursor-not-allowed",
          ].join(" ")}
          onClick={onAddDocument}
        >
          <i className="ri-add-line text-sm" />
          Add Document
        </button>
      </div>
    </CollapsibleSection>
  );
}
