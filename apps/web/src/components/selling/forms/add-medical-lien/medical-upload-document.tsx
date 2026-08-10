import React, { useEffect, useRef, useState } from "react";
import { useSessionContext } from "@/providers/session-provider";
import { useSession } from "@/hooks/use-session";
import { liensService } from "@/lib/selling";
import { documentsService } from "@/lib/documents";
import UploadDocumentComponent, {
  FileDropzoneRef,
} from "@/components/lien/upload-document";
import Field from "@/components/lien/field";
import { ConfirmDialog } from "@/components/lien/modal";
import { useToast } from "@/lib/toast-context";
import { parseDocumentReference } from "@/lib/selling/selling-detail.mapper";
import type { SellingDocumentReferenceRequest } from "@/lib/selling/liens.types";

export interface UploadDocumentsProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  onUploaded?: (valid: boolean, data?: any) => void;
}

interface AttachedDoc {
  documentId: string;
  documentType: string;
  displayName: string;
  createdAt: string;
  fileSize: string;
}

function getFileIcon(filename: string): string {
  const ext = filename.split(".").pop()?.toLowerCase() ?? "";
  if (ext === "pdf") return "ri-file-pdf-2-line";
  if (["doc", "docx"].includes(ext)) return "ri-file-word-2-line";
  if (["xls", "xlsx", "csv"].includes(ext)) return "ri-file-excel-2-line";
  if (["jpg", "jpeg", "png", "gif", "webp"].includes(ext))
    return "ri-image-line";
  return "ri-file-text-line";
}

function fileExtLabel(filename: string): string {
  return filename.split(".").pop()?.toUpperCase() ?? "FILE";
}

function toDocumentRefs(
  docs: AttachedDoc[],
): SellingDocumentReferenceRequest[] {
  return docs.map((doc) => ({
    documentId: doc.documentId,
    documentType: doc.documentType,
    displayName: doc.displayName,
  }));
}

export default function UploadDocuments(props: UploadDocumentsProps) {
  const { lienId, onUploaded, onFormValid } = props;
  const { lookup } = useSessionContext();
  const { session } = useSession();
  const { show: showToast } = useToast();
  const dropzoneRef = useRef<FileDropzoneRef>(null);

  const documentTypes = lookup?.DocumentCategory ?? [];
  const documentTypeOptions = documentTypes.map((d) => ({
    key: d.id,
    value: d.id,
    label: d.name,
  }));

  const [documentTypeId, setDocumentTypeId] = useState("");
  const [docs, setDocs] = useState<AttachedDoc[]>([]);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [showDropzone, setShowDropzone] = useState(true);
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  // A draft lien may already have documents attached from a previous visit —
  // hydrate from the lien itself (same source sell-lien-wizard/portfolio-details
  // use) rather than assuming this step always starts empty.
  useEffect(() => {
    let cancelled = false;
    if (!lienId) {
      setLoading(false);
      return;
    }
    (async () => {
      try {
        const lien = await liensService.getLienById(lienId);
        const enriched = await Promise.all(
          lien.documents.map(async (doc) => {
            const parsed = parseDocumentReference(doc);
            if (!parsed.documentId) return null;
            try {
              const detail = await documentsService.getById(
                parsed.documentId,
              );
              return {
                documentId: parsed.documentId,
                documentType: parsed.documentType,
                displayName: parsed.displayName ?? detail.title,
                createdAt: detail.createdAt,
                fileSize: detail.fileSize,
              } as AttachedDoc;
            } catch {
              return null;
            }
          }),
        );
        if (cancelled) return;
        const resolved = enriched.filter((d): d is AttachedDoc => !!d);
        setDocs(resolved);
        setShowDropzone(resolved.length === 0);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [lienId]);

  useEffect(() => {
    onFormValid?.(true, docs);
    onUploaded?.(true, docs);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [docs]);

  const handleFilesSelected = async (files: File[]) => {
    const file = files[0];
    if (!file) return;

    if (!documentTypeId) {
      showToast("Select a document type first", "error");
      dropzoneRef.current?.reset();
      return;
    }

    setUploading(true);
    try {
      const uploaded = await documentsService.upload({
        file,
        tenantId: session?.tenantId ?? "",
        productId: "SYNQ_LIENS",
        referenceType: "Lien",
        referenceId: lienId ?? "",
        documentTypeId,
        title: file.name,
      });
      const documentType =
        documentTypes.find((t) => t.id === documentTypeId)?.name ??
        documentTypeId;
      const nextDocs: AttachedDoc[] = [
        ...docs,
        {
          documentId: uploaded.id,
          documentType,
          displayName: file.name,
          createdAt: uploaded.createdAt,
          fileSize: uploaded.fileSize,
        },
      ];
      // Persist immediately so the attachment survives navigating away — the
      // wizard's final step doesn't re-save documents, it just confirms.
      await liensService.saveDocuments(lienId ?? "", {
        documents: toDocumentRefs(nextDocs),
      });
      setDocs(nextDocs);
      setShowDropzone(false);
      showToast("Document uploaded.", "success");
    } catch (err) {
      showToast(
        err instanceof Error ? err.message : "Failed to upload document",
        "error",
      );
    } finally {
      dropzoneRef.current?.reset();
      setUploading(false);
    }
  };

  const runDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      const remaining = docs.filter((d) => d.documentId !== deleteTarget);
      await liensService.saveDocuments(lienId ?? "", {
        documents: toDocumentRefs(remaining),
      });
      setDocs(remaining);
      setDeleteTarget(null);
      showToast("Document removed.", "success");
    } catch (err) {
      showToast(
        err instanceof Error ? err.message : "Failed to remove document",
        "error",
      );
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="container-fluid">
      <div className="row border-bottom border-solid pb-3 mb-3">
        <div className="col-12 mb-2">
          <span className="font-semibold mb-2 text-2xl mt-1">
            Upload Documents
          </span>
          <p className="font-normal text-sm text-gray-600 mb-2 mt-1">
            Upload supporting documents to provide additional information for
            this lien.
          </p>
        </div>

        <Field
          label="Document Type"
          required
          value={documentTypeId}
          options={documentTypeOptions}
          onChange={(v: string) => setDocumentTypeId(v)}
          placeholder="Select document type"
          type="select"
        />

        {!loading && docs.length > 0 && (
          <div className="mt-4 space-y-3">
            {docs.map((doc) => (
              <div
                key={doc.documentId}
                className="flex items-center gap-3 border border-gray-200 rounded-lg px-4 py-3"
              >
                <div className="w-10 h-10 rounded bg-gray-50 border border-gray-100 flex items-center justify-center shrink-0 text-gray-500">
                  <i className={`${getFileIcon(doc.displayName)} text-lg`} />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-gray-800 truncate">
                    {doc.displayName}
                  </p>
                  <p className="text-xs text-gray-400 truncate">
                    {doc.documentType} · {fileExtLabel(doc.displayName)}
                  </p>
                </div>
                {doc.createdAt && (
                  <p className="text-xs text-gray-400 shrink-0 whitespace-nowrap">
                    {new Date(doc.createdAt).toLocaleDateString()} ·{" "}
                    {new Date(doc.createdAt).toLocaleTimeString()}
                  </p>
                )}
                <button
                  type="button"
                  onClick={() => setDeleteTarget(doc.documentId)}
                  className="w-8 h-8 flex items-center justify-center rounded-lg border border-red-100 text-red-500 hover:bg-red-50 shrink-0"
                  aria-label="Delete document"
                >
                  <i className="ri-delete-bin-6-line text-sm" />
                </button>
              </div>
            ))}
          </div>
        )}

        {showDropzone ? (
          <div className="mt-4">
            <UploadDocumentComponent
              ref={dropzoneRef}
              isMultiple={false}
              onUploaded={handleFilesSelected}
            />
            {uploading && (
              <p className="text-xs text-gray-400 mt-2">Uploading...</p>
            )}
          </div>
        ) : (
          <button
            type="button"
            disabled={uploading}
            onClick={() => setShowDropzone(true)}
            className="mt-4 inline-flex items-center gap-1.5 text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-700 disabled:opacity-50"
          >
            Upload More
            <i className="ri-upload-cloud-2-line text-sm" />
          </button>
        )}
      </div>

      <ConfirmDialog
        open={deleteTarget !== null}
        onClose={() => setDeleteTarget(null)}
        onConfirm={runDelete}
        loading={deleting}
        title="Remove Document"
        description="This document will no longer be attached to this lien."
        confirmLabel="Remove"
        confirmVariant="danger"
      />
    </div>
  );
}
