import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { CollapsibleSection } from "../../../components/collapsible-section";
import type { DocumentType } from "../types";

export function LienDocumentsSection({
  liensDocuments,
  onDownload,
  onDelete,
}: {
  liensDocuments: DocumentType[];
  onDownload: (url: string) => void;
  onDelete: (id: string) => void;
}) {
  const liensDocumentsColumns: ColumnDef<DocumentType, any>[] = [
    {
      id: "filename",
      header: "Name",
      cell: ({ row }) => (
        <div className="flex items-center gap-2">
          <i className="ri-file-line text-sm text-gray-400" />
          <span className="text-sm text-gray-700 truncate max-w-[200px]">
            {row.original.filename}
          </span>
        </div>
      ),
    },
    {
      id: "documentType",
      header: "Document Type",
      cell: ({ row }) => (
        <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">
          {row.original.documentType}
        </span>
      ),
    },
    {
      id: "updated",
      header: "Last Update",
      cell: ({ row }) => (
        <span className="text-xs text-gray-500 whitespace-nowrap">
          {row.original.updated}
        </span>
      ),
    },
    {
      id: "action",
      header: "Action",
      meta: { align: "right" },
      cell: ({ row }) => (
        <div className="inline-flex items-center gap-1">
          <button
            className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-primary transition-colors"
            title="Download"
            onClick={() => onDownload(row.original.url)}
          >
            <i className="ri-download-2-line text-sm" />
          </button>
          <button
            className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-red-500 transition-colors"
            title="Delete"
            onClick={() => onDelete(row.id)}
          >
            <i className="ri-delete-bin-6-line text-sm" />
          </button>
        </div>
      ),
    },
  ];

  return (
    <CollapsibleSection title="Lien Documents" icon="ri-attachment-2">
      {liensDocuments.length === 0 ? (
        <div className="text-center py-8">
          <i className="ri-attachment-2 text-2xl text-gray-300" />
          <p className="text-sm text-gray-400 mt-2">
            No lien documents available
          </p>
        </div>
      ) : (
        <>
          <BaseTable
            columns={liensDocumentsColumns}
            data={liensDocuments}
            getRowId={(doc) => doc.id}
            enablePagination={false}
          />
          <div className="mt-3 flex items-center justify-between">
            <p className="text-xs text-gray-400">
              {liensDocuments.length} document
              {liensDocuments.length !== 1 ? "s" : ""}
            </p>
          </div>
        </>
      )}
    </CollapsibleSection>
  );
}
