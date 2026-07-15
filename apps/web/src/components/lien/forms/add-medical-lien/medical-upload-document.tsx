import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import Field from "../../field";
import { lookupService } from "@/lib/lookup";
import { facilityService } from "@/lib/facility";
import { UploadDocumentForm } from "../upload-document-form";
import { useSessionContext } from "@/providers/session-provider";
import UploadDocumentComponent, {
  FileDropzoneRef,
} from "../../upload-document";
import { casesService } from "@/lib/cases";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import { ConfirmDialog } from "../../modal";

export interface UploadDocumentsProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  openAddFundingCompanyModal?: () => void;
  onUploaded?: (valid: boolean, data?: any) => void;
}

const INITIAL_FORM = {
  documentType: "",
};

type UploadForm = {
  documentType: string;
  document?: File | File[] | null;
  [key: string]: any;
};

const TEMP_CASE_DOCUMENTS = [
  {
    id: "doc-1",
    name: "Medical_Records_Regional_Hospital.pdf",
    documentType: "Medical Records",
    lastUpdate: "04/12/2026",
    size: "2.4 MB",
  },
  {
    id: "doc-2",
    name: "Billing_Statement_March_2026.pdf",
    documentType: "Billing Statement",
    lastUpdate: "04/10/2026",
    size: "840 KB",
  },
  {
    id: "doc-3",
    name: "Demand_Letter_v2.docx",
    documentType: "Demand Letter",
    lastUpdate: "04/08/2026",
    size: "156 KB",
  },
  {
    id: "doc-4",
    name: "Insurance_Response_StateFarm.pdf",
    documentType: "Insurance Correspondence",
    lastUpdate: "04/05/2026",
    size: "1.1 MB",
  },
];

type DropdownData = {
  status: Array<Record<string, string>>;
};

export default function UploadDocuments(props: UploadDocumentsProps) {
  const { data = {}, onUploaded, lienId } = props;
  const { lookup } = useSessionContext();
  const addToast = useLienStore((s) => s.addToast);
  const dropzoneRef = useRef<FileDropzoneRef>(null);

  const initialForm: UploadForm = {
    ...INITIAL_FORM,
    ...(data as Partial<UploadForm>),
  };
  const [form, setForm] = useState<UploadForm>(initialForm);

  const [errors, setErrors] = useState<Record<string, string>>({});
  const [confirmAction, showConfirmAction] = useState<{
    id: string;
    isOpen: boolean;
  }>({ id: "", isOpen: false });

  const documentTypes =
    lookup?.DocumentCategory.map((d) => {
      return {
        key: d.id,
        value: d.id,
        label: d.name,
      };
    }) ?? [];
  const [documents, setDocuments] = useState<any[]>(data);
  const [files, setFiles] = useState<File[] | null>(null);

  useEffect(() => {}, [data]);

  function getFileIcon(filename: string): string {
    const ext = filename.split(".").pop()?.toLowerCase() ?? "";
    if (ext === "pdf") return "ri-file-pdf-2-line";
    if (["doc", "docx"].includes(ext)) return "ri-file-word-2-line";
    if (["xls", "xlsx"].includes(ext)) return "ri-file-excel-2-line";
    if (["jpg", "jpeg", "png", "gif", "webp"].includes(ext))
      return "ri-image-line";
    return "ri-file-text-line";
  }

  function download(file: any) {
    window.open(file || URL.createObjectURL(file as any), "_blank");
  }

  async function deleteFileConfimation(fileId: string) {
    showConfirmAction({ isOpen: true, id: fileId });
  }
  const deleteFile = useCallback(async () => {
    try {
      await casesService.deleteLiensDocument(confirmAction.id);
      fetchDocument();
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Delete Failed",
          description: err.message,
        });
      }
    }
  }, [confirmAction]);

  const uploadLiensDocuments = async () => {
    if (!files || files.length == 0) return;
    try {
      files.forEach(async (element: File) => {
        const formData = new FormData();
        formData.append("File", element ?? "");
        formData.append("liensId", lienId ?? "");
        formData.append("DocName", element.name);
        formData.append("DocDescription", "Legacy Lien Document upload");
        formData.append("DocFileTypeId", form.documentType);

        await casesService.uploadLiensDocuments(formData);
        addToast({
          type: "success",
          title: "Document Uploaded",
          description: `Document has been Uploaded.`,
        });
        dropzoneRef?.current?.reset();
        setForm(initialForm);
        props?.onUploaded?.(true, "");
        props.onFormValid?.(true, "");
      });
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Upload Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Upload Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const fetchDocument = async () => {
    const docs = await casesService.loadLiensDocuments(lienId ?? "");
    setDocuments(
      docs.data.map((d: any) => {
        return {
          name: d.filename,
          type: d.typeId,
          id: d.id,
          url: d.url,
        };
      }),
    );
  };
  const getDocumentNameById = (id: string) => {
    return documentTypes.find((t) => t.key == id)?.label;
  };
  useEffect(() => {
    if (data?.length > 0) {
      setDocuments(
        data.map((d: any) => {
          return {
            name: d.filename,
            type: d.typeId,
            id: d.id,
            url: d.url,
          };
        }),
      );
    } else {
      fetchDocument();
    }
  }, [data, onUploaded]);

  return (
    <div className="container-fluid">
      <div className="row border-bottom border-solid pb-3 mb-3">
        <div className="col-12 mb-2">
          <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-primary">
            <i className="ri-stethoscope-line text-light" />
          </span>
          <span className="font-semibold mb-2 mt-1">Upload Document</span>
        </div>
        <Field
          label="Document Type"
          required
          value={form.documentType}
          options={documentTypes}
          onChange={(v: string) =>
            setForm({ ...form, documentType: v.toString() })
          }
          error={errors.documentType}
          placeholder=""
          type="select"
        />
        <div className="mt-4">
          <UploadDocumentComponent
            ref={dropzoneRef}
            onUploaded={(e: File[] | null) => {
              setFiles(e);
            }}
          />
          <button
            disabled={files != null && !form.documentType}
            className={[
              "w-full mt-3 mb-5 px-4 py-2.5 text-sm font-medium rounded-lg transition-colors flex items-center justify-center gap-2",
              files && form.documentType
                ? "bg-primary text-white hover:bg-primary/90"
                : "bg-gray-100 text-gray-400 cursor-not-allowed",
            ].join(" ")}
            onClick={() => {
              uploadLiensDocuments();
            }}
          >
            <i className="ri-add-line text-sm" />
            Add Document
          </button>
        </div>

        {documents?.length === 0 ? (
          <div className="text-center py-8">
            <i className="ri-file-copy-2-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">
              No case documents uploaded
            </p>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto -mx-5 px-5">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100">
                    <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">
                      Name
                    </th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Document Type
                    </th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Last Update
                    </th>
                    <th className="pl-3 py-2 text-center text-[11px] font-medium text-gray-400 uppercase tracking-wide w-[80px]">
                      Action
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {documents?.length > 0 &&
                    documents?.map((doc) => (
                      <tr
                        key={doc.id}
                        className="hover:bg-gray-50/50 transition-colors"
                      >
                        <td className="pr-3 py-2.5">
                          <div className="flex items-center gap-2">
                            <i
                              className={`ri-file-line text-sm text-gray-400`}
                            />
                            <span className="text-sm text-gray-700 truncate max-w-[200px]">
                              {doc.name}
                            </span>
                          </div>
                        </td>
                        <td className="px-3 py-2.5">
                          <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">
                            {getDocumentNameById(doc.type)}
                          </span>
                        </td>
                        <td className="px-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                          {doc.updated}
                        </td>
                        <td className="pl-3 py-2.5 text-center">
                          <div className="inline-flex items-center gap-1">
                            <button
                              className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-primary transition-colors"
                              title="Download"
                              onClick={() => download(doc.url)}
                            >
                              <i className="ri-download-2-line text-sm" />
                            </button>
                            <button
                              className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-red-500 transition-colors"
                              title="Delete"
                              onClick={() => deleteFileConfimation(doc.id)}
                            >
                              <i className="ri-delete-bin-6-line text-sm" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
            <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
              <p className="text-xs text-gray-400">
                {documents?.length} document
                {documents?.length !== 1 ? "s" : ""}
              </p>
            </div>
          </>
        )}
      </div>

      {confirmAction.isOpen && (
        <ConfirmDialog
          open
          onClose={() => showConfirmAction({ id: "", isOpen: false })}
          onConfirm={deleteFile}
          title="Delete Document"
          description={
            <>
              Are you sure you want to delete document? This action cannot be
              undone and will permanently remove the document.
            </>
          }
          confirmLabel="Delete"
          confirmVariant="danger"
        />
      )}
    </div>
  );
}
