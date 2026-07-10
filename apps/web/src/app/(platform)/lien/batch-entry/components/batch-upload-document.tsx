import Field from "@/components/lien/field";
import UploadDocumentComponent from "@/components/lien/upload-document";
import { useCallback, useEffect, useState } from "react";

type templateItem = {
  id: string;
  code: string;
  name: string;
  icon: string;
  color: string;
};
type templateData = {
  caseId?: string;
  template: string;
  templateLabel: string;
  file: File;
};
type BatchUploadComponentProps = {
  templateList: templateItem[];
  onTemplateUpdate: (data: templateData) => void;
  onDownload: (id: string) => void;
};
export default function BatchUploadDocumentComponent({
  templateList,
  onDownload,
  onTemplateUpdate,
}: BatchUploadComponentProps) {
  const [selectedTemplate, setSelectedTemplate] = useState<string | null>();
  const [templateLabel, setTemplateLabel] = useState<string | null>();
  const [caseId, setCaseId] = useState<string | null>();
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  const onUploaded = useCallback((e: File[]) => {
    setSelectedFile(e[0]);
  }, []);

  useEffect(() => {
    console.log(caseId, templateLabel, selectedTemplate, selectedFile);
    if (templateLabel && selectedTemplate && selectedFile) {
      onTemplateUpdate({
        caseId: caseId ?? "",
        template: selectedTemplate,
        templateLabel: templateLabel,
        file: selectedFile,
      });
    }
  }, [templateLabel, selectedTemplate, selectedFile]);

  return (
    <div className="space-y-6">
      <Field
        label="Label"
        required
        value={templateLabel ?? ""}
        onChange={(v) => setTemplateLabel(v.toString())}
        placeholder=""
      />
      {selectedTemplate == "ADD_PAYMENTS_EXISTING_LIENS" && (
        <Field
          label="Case Id"
          required
          value={caseId ?? ""}
          onChange={(v) => setCaseId(v.toString())}
          placeholder="Enter Case Id"
        />
      )}

      <h3 className="text-sm font-semibold text-gray-800 mb-3">
        Documents <span className="text-red-500 ml-0.5">*</span>
      </h3>

      <UploadDocumentComponent
        config={{ isMultiple: false, accepted: ".csv" }}
        // ref={dropzoneRef}
        onUploaded={(e) => onUploaded(e)}
      />

      <div className="border border-gray-200 rounded-xl p-5">
        <h3 className="text-sm font-semibold text-gray-800 mb-3">Templates</h3>
        <p className="text-xs text-gray-600 mb-4">Select Template</p>
        <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
          {templateList.map((t) => (
            <div
              key={t.id}
              className={`flex items-center gap-3 p-3 border border-gray-100 rounded-lg text-gray-700 transition-colors text-left hover:cursor-pointer hover:bg-gray-100   ${selectedTemplate == t.code ? "bg-gray-100 border-gray-500" : ""}`}
              onClick={(e) => {
                e.preventDefault();
                setSelectedTemplate(t.code);
              }}
            >
              <i className={`${t.icon} text-lg ${t.color}`} />
              <div className="flex-1">
                <p className="text-sm font-medium">{t.name}</p>
              </div>
              <button
                className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100  hover:text-primary transition-colors"
                title="Download"
                onClick={() => onDownload(t.code)}
              >
                <i className="ri-download-2-line text-sm" />
              </button>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
