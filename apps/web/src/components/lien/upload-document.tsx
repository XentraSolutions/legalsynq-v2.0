import { useEffect, useRef, useState } from "react";

export interface UploadDocumentComponentProps {
  onUploaded: (e: File | null) => void;
}

export function UploadDocumentComponent({
  onUploaded,
}: UploadDocumentComponentProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [form, setForm] = useState({
    title: "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [dragOver, setDragOver] = useState(false);
  const [uploading, setUploading] = useState(false);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!file) e.file = "File is required";
    if (!form.title.trim()) e.title = "Title is required";
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleFileSelect = (files: FileList | null) => {
    if (files && files.length > 0) {
      const selected = files[0];
      setFile(selected);
      if (!form.title.trim()) {
        setForm((f) => ({ ...f, title: selected.name }));
      }
    }
  };

  useEffect(() => {
    onUploaded(file);
  }, [file]);
  return (
    <>
      <div className="space-y-4">
        <input
          ref={fileInputRef}
          type="file"
          className="hidden"
          accept=".pdf,.jpg,.png,.docx,.xlsx,.xls,.csv"
          onChange={(e) => handleFileSelect(e.target.files)}
          multiple
        />
        <div
          className={`border-2 border-dashed rounded-xl p-8 text-center transition-colors cursor-pointer ${dragOver ? "border-primary bg-primary/5" : file ? "border-green-300 bg-green-50" : "border-gray-200"}`}
          onClick={() => fileInputRef.current?.click()}
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragOver(false);
            handleFileSelect(e.dataTransfer.files);
          }}
        >
          {file ? (
            <div className="flex items-center justify-center gap-2">
              <i className="ri-file-text-line text-2xl text-green-600" />
              <div className="text-left">
                <span className="text-sm font-medium text-gray-700">
                  {file.name}
                </span>
                <p className="text-xs text-gray-400">
                  {(file.size / 1024).toFixed(0)} KB
                </p>
              </div>
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  setFile(null);
                }}
                className="text-gray-400 hover:text-gray-600"
              >
                <i className="ri-close-line" />
              </button>
            </div>
          ) : (
            <>
              <i className="ri-upload-cloud-2-line text-3xl text-gray-300 mb-2" />
              <p className="text-sm text-gray-500">
                Click or drag file to upload
              </p>
              <p className="text-xs text-gray-400 mt-1">
                ".pdf,.jpg,.png,.docx,.xlsx,.xls,.csv" (max 10MB)
              </p>
            </>
          )}
        </div>
        {errors.file && <p className="text-xs text-red-500">{errors.file}</p>}
      </div>
    </>
  );
}
