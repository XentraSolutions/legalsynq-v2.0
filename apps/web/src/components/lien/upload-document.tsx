import {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useState,
} from "react";
import { FileRejection, useDropzone, type Accept } from "react-dropzone";

export interface UploadDocumentComponentProps {
  onUploaded: (files: File[]) => void;
  isMultiple?: boolean;
  config?: {
    isMultiple?: boolean;
    accepted?: string | Accept;
  };
}

export interface FileDropzoneRef {
  reset: () => void;
}

const DEFAULT_ACCEPTED_FILES: Accept = {
  "application/pdf": [".pdf"],
  "image/*": [".jpg", ".jpeg", ".png"],
  "text/csv": [".csv"],
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [
    ".xlsx",
  ],
  "application/vnd.ms-excel": [".xls"],
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document": [
    ".docx",
  ],
};

const UploadDocumentComponent = forwardRef<
  FileDropzoneRef,
  UploadDocumentComponentProps
>(({ onUploaded, isMultiple, config }, ref) => {
  const [files, setFiles] = useState<File[]>([]);
  const [errorMessage, setErrorMessage] = useState("");

  const onDrop = useCallback(
    (acceptedFiles: File[]) => {
      const updatedFiles = [...files, ...acceptedFiles];

      setFiles(updatedFiles);
      onUploaded(updatedFiles);
      setErrorMessage("");
    },
    [files, onUploaded],
  );

  useImperativeHandle(ref, () => ({
    reset() {
      setFiles([]);
      onUploaded([]);
    },
  }));

  const multiple = config?.isMultiple ?? isMultiple ?? true;
  const acceptedFiles = config?.accepted ?? DEFAULT_ACCEPTED_FILES;
  const acceptedLabel =
    typeof acceptedFiles === "string"
      ? acceptedFiles
      : ".pdf,.jpg,.jpeg,.png,.csv,.xlsx,.xls,.docx";
  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    onDropRejected: (rejectedFiles) => {
      // Fallback hook callback if you prefer handling errors on-drop
      handleErrors(rejectedFiles);
    },
    multiple,
    accept: acceptedFiles as Accept,
    maxSize: 50 * 1024 * 1024,
  });

  const handleErrors = (rejections: FileRejection[]) => {
    rejections.forEach((rejection) => {
      rejection.errors.forEach((err) => {
        if (err.code === "file-too-large") {
          setErrorMessage("This file is too large. Max size allowed is 5MB.");
        } else {
          setErrorMessage(err.message);
        }
      });
    });
  };

  useEffect(() => {}, [errorMessage]);
  const removeFile = (index: number) => {
    const updatedFiles = files.filter((_, i) => i !== index);

    setFiles(updatedFiles);
    onUploaded(updatedFiles);
  };

  return (
    <section>
      <div
        {...getRootProps()}
        className="border-2 border-dashed rounded-xl p-8 text-center transition-colors cursor-pointer border-gray-200"
      >
        <input {...getInputProps()} />

        <i className="ri-upload-cloud-2-line text-3xl text-gray-300 mb-2" />
        <p className="text-sm text-gray-500">Click or drag file to upload</p>
        <p className="text-xs text-gray-400 mt-1">{acceptedLabel} (max 10MB)</p>
      </div>
      {errorMessage && (
        <p className="text-red-500 bg-red-100/80 rounded-md p-4 my-3 text-sm whitespace-pre-line break-all">
          <i className="ri-error-warning-line"></i> {errorMessage}
        </p>
      )}
      {files.length > 0 ? (
        <div className="w-full my-4">
          {files.map((file, index) => (
            <div
              className={`flex items-center gap-2 p-2 ${index != files.length - 1 ? "border-b-1 border-gray-300" : ""} `}
              key={index}
            >
              <i className="ri-file-text-line text-gray-400" />
              <div className="text-left flex flex-1 items-center ">
                <span className="text-sm font-medium text-gray-700">
                  {file.name}
                </span>
                <p className="text-xs text-gray-400 ml-2">
                  {(file.size / 1024).toFixed(0)} KB
                </p>
              </div>
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  removeFile(index);
                }}
                className="text-gray-400 hover:text-gray-600"
              >
                <i className="ri-close-line" />
              </button>
            </div>
          ))}
        </div>
      ) : (
        <></>
      )}
    </section>
  );
});

export default UploadDocumentComponent;
