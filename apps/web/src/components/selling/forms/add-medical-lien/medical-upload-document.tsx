import React, { useEffect, useRef, useState } from "react";
import { Loader } from "lucide-react";
import { useSessionContext } from "@/providers/session-provider";
import { useSession } from "@/hooks/use-session";
import { liensService } from "@/lib/selling";
import { documentsService } from "@/lib/documents";
import UploadDocumentComponent, {
  FileDropzoneRef,
} from "@/components/selling/upload-document";
import {
  fileExtLabel,
  fileIconFor,
  UploadedFileRow,
} from "@/components/selling/uploaded-file-row";
import Field from "@/components/lien/field";
import { ConfirmDialog } from "@/components/selling/modal";
import { toast } from "sonner";
import { parseDocumentReference } from "@/lib/selling/selling-detail.mapper";
import type { SellingDocumentReferenceRequest } from "@/lib/selling/liens.types";
import { Button } from "@/components/selling/button";

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

function toDocumentRefs(
  docs: AttachedDoc[],
): SellingDocumentReferenceRequest[] {
  return docs.map((doc) => ({
    documentId: doc.documentId,
    documentType: doc.documentType,
    displayName: doc.displayName,
  }));
}

function byNewestFirst(docs: AttachedDoc[]): AttachedDoc[] {
  return [...docs].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
  );
}

export default function UploadDocuments(props: UploadDocumentsProps) {
  const { lienId, onUploaded, onFormValid } = props;
  const { lookup } = useSessionContext();
  const { session } = useSession();
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
  const [pendingFileName, setPendingFileName] = useState<string | null>(null);
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
              const detail = await documentsService.getById(parsed.documentId);
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
        const resolved = byNewestFirst(
          enriched.filter((d): d is AttachedDoc => !!d),
        );
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
      toast.error("Select a document type first");
      dropzoneRef.current?.reset();
      return;
    }

    setPendingFileName(file.name);
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
      const nextDocs: AttachedDoc[] = byNewestFirst([
        ...docs,
        {
          documentId: uploaded.id,
          documentType,
          displayName: file.name,
          createdAt: uploaded.createdAt,
          fileSize: uploaded.fileSize,
        },
      ]);
      // Persist immediately so the attachment survives navigating away — the
      // wizard's final step doesn't re-save documents, it just confirms.
      await liensService.saveDocuments(lienId ?? "", {
        documents: toDocumentRefs(nextDocs),
      });
      setDocs(nextDocs);
      setShowDropzone(false);
      setDocumentTypeId("");
      toast.success("Document uploaded.");
    } catch (err) {
      toast.error(
        err instanceof Error ? err.message : "Failed to upload document",
      );
    } finally {
      dropzoneRef.current?.reset();
      setPendingFileName(null);
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
      toast.success("Document removed.");
    } catch (err) {
      toast.error(
        err instanceof Error ? err.message : "Failed to remove document",
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
          clearable
        />
        <div className="mt-4">
          <UploadDocumentComponent
            ref={dropzoneRef}
            isMultiple={false}
            disabled={!documentTypeId}
            onUploaded={handleFilesSelected}
          />
        </div>
        {!loading && (docs.length > 0 || pendingFileName) && (
          <div className="mt-4 space-y-3 max-h-70 overflow-y-auto pr-1">
            {pendingFileName && (
              <UploadedFileRow
                icon={fileIconFor(pendingFileName)}
                title={pendingFileName}
                subtitle="Uploading..."
                actions={
                  <Loader className="w-4 h-4 text-gray-400 animate-spin" />
                }
              />
            )}
            {docs.map((doc) => (
              <UploadedFileRow
                key={doc.documentId}
                icon={fileIconFor(doc.displayName)}
                title={doc.displayName}
                subtitle={`${doc.documentType} · ${fileExtLabel(doc.displayName)}`}
                timestamp={doc.createdAt}
                actions={
                  <Button
                    type="button"
                    variant="icon-square-destructive"
                    className="w-8 h-8"
                    icon="trash2"
                    onClick={() => setDeleteTarget(doc.documentId)}
                    aria-label="Delete document"
                  />
                }
              />
            ))}
          </div>
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
