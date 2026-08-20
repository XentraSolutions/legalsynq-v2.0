import React, { useEffect, useRef, useState } from "react";
import { Loader } from "lucide-react";
import { useSessionContext } from "@/providers/session-provider";
import { useSession } from "@/hooks/use-session";
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
import { SkeletonFileRow } from "@/components/lien/skeleton-loader";
import { ConfirmDialog } from "@/components/selling/modal";
import { toast } from "sonner";
import {
  sortByNewestFirst,
  useLienDocuments,
  useSaveLienDocuments,
} from "@/lib/selling/use-lien-documents";
import { Button } from "@/components/selling/button";
import { sellingLookupsApi } from "@/lib/selling/lookup.api";
import {
  camelCaseToLabel,
  resolveDocumentCategory,
} from "@/lib/selling/selling-detail.mapper";

export interface UploadDocumentsProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  onUploaded?: (valid: boolean, data?: any) => void;
  /** Hides the built-in "Upload Documents" heading/description — for callers (e.g. a modal) that already render their own title. */
  hideHeading?: boolean;
  /** Skips displaying documents already attached to the lien — for callers (e.g. a modal launched from a page that already lists them) that should only show files uploaded in this session. The full set is still fetched/persisted so saves append rather than overwrite. */
  hideExistingDocuments?: boolean;
}

export default function UploadDocuments(props: UploadDocumentsProps) {
  const { lienId, onUploaded, onFormValid, hideHeading, hideExistingDocuments } =
    props;
  const { lookup } = useSessionContext();
  const { session } = useSession();
  const dropzoneRef = useRef<FileDropzoneRef>(null);

  // The document-type dropdown is restricted to the selling domain's own
  // fixed enum (GET /selling/lookups/document-types) rather than the full
  // DocumentCategory catalog, since only those values are valid on a sale's
  // saveDocuments payload. Each selection is then matched to the closest
  // DocumentCategory row to get a real documentTypeId for the upload.
  const documentCategories = lookup?.DocumentCategory ?? [];
  const [sellingDocumentTypes, setSellingDocumentTypes] = useState<string[]>(
    [],
  );
  useEffect(() => {
    let cancelled = false;
    sellingLookupsApi
      .documentTypes()
      .then((res) => {
        if (!cancelled) setSellingDocumentTypes(res.data.items);
      })
      .catch(() => {
        // Non-fatal — the dropdown just stays empty and upload stays disabled.
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const documentTypeOptions = sellingDocumentTypes.map((type) => ({
    key: type,
    value: type,
    label: camelCaseToLabel(type),
  }));

  const [documentType, setDocumentType] = useState("");
  const { data: docs = [], isLoading: loading } = useLienDocuments(lienId);
  const saveLienDocuments = useSaveLienDocuments(lienId);
  // Uploaded during this component's lifetime — when hideExistingDocuments is
  // set, only these render, while the full cached list still backs saves.
  const [newDocumentIds, setNewDocumentIds] = useState<Set<string>>(new Set());
  const [pendingFileName, setPendingFileName] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    onFormValid?.(true, docs);
    onUploaded?.(true, docs);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [docs]);

  const handleFilesSelected = async (files: File[]) => {
    const file = files[0];
    if (!file) return;

    if (!documentType) {
      toast.error("Select a document type first");
      dropzoneRef.current?.reset();
      return;
    }

    const documentTypeId = resolveDocumentCategory(
      documentType,
      documentCategories,
    )?.id;
    if (!documentTypeId) {
      toast.error("Document type list is still loading. Please try again.");
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
      const documentTypeLabel = camelCaseToLabel(documentType);
      // Persist immediately so the attachment survives navigating away — the
      // wizard's final step doesn't re-save documents, it just confirms.
      // Appends onto the latest server-side list (fetched inside
      // saveLienDocuments), not the possibly-stale `docs` in local state.
      await saveLienDocuments((current) =>
        sortByNewestFirst([
          ...current,
          {
            documentId: uploaded.id,
            documentType: documentTypeLabel,
            displayName: file.name,
            createdAt: uploaded.createdAt,
            fileSize: uploaded.fileSize,
          },
        ]),
      );
      setNewDocumentIds((prev) => new Set(prev).add(uploaded.id));
      setDocumentType("");
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
      await saveLienDocuments((current) =>
        current.filter((d) => d.documentId !== deleteTarget),
      );
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

  const visibleDocs = hideExistingDocuments
    ? docs.filter((doc) => newDocumentIds.has(doc.documentId))
    : docs;

  return (
    <div className="container-fluid">
      <div className="row border-bottom border-solid pb-3 mb-3">
        {!hideHeading && (
          <div className="col-12 mb-2">
            <span className="font-semibold mb-2 text-2xl mt-1">
              Upload Documents
            </span>
            <p className="font-normal text-sm text-gray-600 mb-2 mt-1">
              Upload supporting documents to provide additional information
              for this lien.
            </p>
          </div>
        )}

        <Field
          label="Document Type"
          required
          value={documentType}
          options={documentTypeOptions}
          onChange={(v: string) => setDocumentType(v)}
          placeholder="Select document type"
          type="select"
          clearable
        />
        <div className="mt-4">
          <UploadDocumentComponent
            ref={dropzoneRef}
            isMultiple={false}
            disabled={!documentType}
            onUploaded={handleFilesSelected}
          />
        </div>
        {loading && (
          <div className="mt-4 space-y-3">
            <SkeletonFileRow />
            <SkeletonFileRow />
          </div>
        )}
        {!loading && (visibleDocs.length > 0 || pendingFileName) && (
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
            {visibleDocs.map((doc) => (
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
