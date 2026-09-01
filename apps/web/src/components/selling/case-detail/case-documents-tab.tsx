"use client";

import { useQueries, useQueryClient } from "@tanstack/react-query";
import { Copy, FileStack } from "lucide-react";
import { toast } from "sonner";
import { useState } from "react";
import { ContactsEmptyState } from "@/components/selling/contacts/contacts-empty-state";
import { ConfirmDialog } from "@/components/selling/modal";
import { fileIconFor, UploadedFileRow } from "@/components/selling/uploaded-file-row";
import { Button } from "@/components/selling/button";
import { SkeletonFileRow } from "@/components/lien/skeleton-loader";
import {
  SALE_DOCUMENT_LABELS,
  camelCaseToLabel,
} from "@/lib/selling/selling-detail.mapper";
import { useCaseLiens } from "@/lib/selling/use-case-liens";
import {
  fetchLienDocuments,
  lienDocumentsQueryKey,
  sortByNewestFirst,
  type LienDocument,
} from "@/lib/selling/use-lien-documents";
import { liensService } from "@/lib/selling/selling-liens.service";
import { ApiError } from "@/lib/api-client";

interface CaseDocument extends LienDocument {
  lienId: string;
}

// There's no case-level document store, only per-lien ones — this fetches
// each of the case's liens' documents in parallel and flattens them into a
// single concatenated list rather than a per-lien grouping, since a case
// can have many liens and repeating the "Documents" panel per lien pushed
// the useful content far down the page.
export function CaseDocumentsTab({ caseId }: { caseId: string }) {
  const queryClient = useQueryClient();
  const { data, isLoading: liensLoading, isError, error } = useCaseLiens(
    caseId,
    { pageSize: 100 },
  );
  const liens = data?.items ?? [];
  const [deleteTarget, setDeleteTarget] = useState<CaseDocument | null>(null);
  const [deleting, setDeleting] = useState(false);

  const docQueries = useQueries({
    queries: liens.map((lien) => ({
      queryKey: lienDocumentsQueryKey(lien.lienId),
      queryFn: () => fetchLienDocuments(lien.lienId),
      staleTime: 0,
      enabled: !liensLoading,
    })),
  });

  const docsLoading = liensLoading || docQueries.some((q) => q.isLoading);
  const docs = sortByNewestFirst(
    docQueries.flatMap((q, i) =>
      (q.data ?? []).map((doc) => ({ ...doc, lienId: liens[i].lienId })),
    ),
  ) as CaseDocument[];

  const runDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      const current = await queryClient.fetchQuery({
        queryKey: lienDocumentsQueryKey(deleteTarget.lienId),
        queryFn: () => fetchLienDocuments(deleteTarget.lienId),
      });
      const next = current.filter(
        (d) => d.documentId !== deleteTarget.documentId,
      );
      await liensService.saveDocuments(deleteTarget.lienId, {
        documents: next.map((d) => ({
          documentId: d.documentId,
          documentType: d.documentType,
          displayName: d.displayName,
        })),
      });
      queryClient.setQueryData(
        lienDocumentsQueryKey(deleteTarget.lienId),
        next,
      );
      toast.success("Document removed.");
      setDeleteTarget(null);
    } catch (err) {
      toast.error(
        err instanceof Error ? err.message : "Failed to remove document",
      );
    } finally {
      setDeleting(false);
    }
  };

  if (liensLoading) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg h-48 animate-pulse" />
    );
  }

  if (isError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
        {error instanceof ApiError ? error.message : "Failed to load liens."}
      </div>
    );
  }

  if (liens.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg">
        <ContactsEmptyState
          icon={FileStack}
          title="No Liens On This Case"
          description="Add a lien to this case to start attaching documents."
        />
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-6 py-5">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-md font-semibold">Documents</h3>
        </div>

        {docsLoading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4">
            <SkeletonFileRow />
            <SkeletonFileRow />
          </div>
        ) : docs.length === 0 ? (
          <div className="py-10 text-center">
            <Copy className="h-6 w-6 text-gray-300 mx-auto" />
            <p className="text-sm text-gray-400 mt-2">
              No documents attached to this case yet
            </p>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4">
            {docs.map((doc) => (
              <UploadedFileRow
                key={doc.documentId}
                icon={fileIconFor(doc.displayName)}
                title={doc.displayName}
                subtitle={
                  SALE_DOCUMENT_LABELS[doc.documentType]?.title ??
                  camelCaseToLabel(doc.documentType)
                }
                timestamp={doc.createdAt}
                actions={
                  <Button
                    variant="icon-square-destructive"
                    className="w-8 h-8"
                    icon="trash2"
                    onClick={() => setDeleteTarget(doc)}
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
