import { useQuery, useQueryClient } from "@tanstack/react-query";
import { liensService } from "./selling-liens.service";
import { documentsService } from "@/lib/documents";
import { parseDocumentReference } from "./selling-detail.mapper";
import type { SellingDocumentReferenceRequest } from "./liens.types";

export interface LienDocument {
  documentId: string;
  documentType: string;
  displayName: string;
  createdAt: string;
  fileSize: string;
}

export function lienDocumentsQueryKey(lienId?: string) {
  return ["lien-documents", lienId] as const;
}

export function sortByNewestFirst(docs: LienDocument[]): LienDocument[] {
  return [...docs].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
  );
}

function toDocumentRefs(docs: LienDocument[]): SellingDocumentReferenceRequest[] {
  return docs.map((doc) => ({
    documentId: doc.documentId,
    documentType: doc.documentType,
    displayName: doc.displayName,
  }));
}

export async function fetchLienDocuments(lienId: string): Promise<LienDocument[]> {
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
        } satisfies LienDocument;
      } catch {
        return null;
      }
    }),
  );
  return sortByNewestFirst(enriched.filter((d): d is LienDocument => d != null));
}

// Shared across the portfolio detail page's Documents tab and the upload
// modal launched from it (and any other lien-document consumer) via the
// same queryKey, so opening the modal reuses whatever the tab already
// fetched instead of hitting the network again *within the same render
// pass*. staleTime is 0 (unlike the app's 60s default) because this lien
// may be open on another machine at the same time — a stale read here isn't
// just a display nit, it's the snapshot a save would silently overwrite the
// server with, so every mount/focus re-checks the server rather than
// trusting a minute-old cache.
export function useLienDocuments(lienId?: string) {
  return useQuery({
    queryKey: lienDocumentsQueryKey(lienId),
    queryFn: () => fetchLienDocuments(lienId as string),
    enabled: !!lienId,
    staleTime: 0,
  });
}

// Persists the document list and writes the result straight into the shared
// cache, so every mounted consumer of useLienDocuments(lienId) re-renders
// immediately. Takes an updater over the *current* list rather than a
// precomputed one: since saveDocuments replaces the whole set (there's no
// partial-update endpoint), building the next list from a snapshot taken
// seconds ago would silently drop another machine's concurrent edit to this
// lien. fetchQuery re-checks the server (staleTime 0 above) immediately
// before merging, which narrows but does not eliminate that race — there's
// still no server-side conflict detection (e.g. an ETag/version check), so
// two saves within the same instant can still race.
export function useSaveLienDocuments(lienId?: string) {
  const queryClient = useQueryClient();
  return async (updater: (current: LienDocument[]) => LienDocument[]) => {
    const current = await queryClient.fetchQuery({
      queryKey: lienDocumentsQueryKey(lienId),
      queryFn: () => fetchLienDocuments(lienId as string),
    });
    const nextDocs = updater(current);
    await liensService.saveDocuments(lienId ?? "", {
      documents: toDocumentRefs(nextDocs),
    });
    queryClient.setQueryData(lienDocumentsQueryKey(lienId), nextDocs);
    return nextDocs;
  };
}
