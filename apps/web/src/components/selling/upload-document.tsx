import {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useState,
} from "react";
import { FileRejection, useDropzone, type Accept } from "react-dropzone";
import { CircleAlert, CloudUpload } from "lucide-react";

export interface SellingUploadDocumentProps {
  onUploaded: (files: File[]) => void;
  isMultiple?: boolean;
  disabled?: boolean;
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
  // CSVs are frequently given Excel's MIME type or text/plain
  "text/csv": [".csv"],
  "text/plain": [".csv"],
  // XLSX fallback MIME types (Excel / Zip formats)
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [
    ".xlsx",
  ],
  "application/octet-stream": [".xlsx", ".xls"],
  "application/x-zip-compressed": [".xlsx"],
  // XLS fallback MIME types
  "application/vnd.ms-excel": [".xls", ".csv"], // Excel often marks CSVs with this!
  "application/msexcel": [".xls"],
  "application/x-msexcel": [".xls"],
  // DOCX
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document": [
    ".docx",
  ],
};

export const SELLING_DOCUMENT_MAX_FILE_SIZE_MB = 25;
export const SELLING_DOCUMENT_MAX_FILE_SIZE_BYTES =
  SELLING_DOCUMENT_MAX_FILE_SIZE_MB * 1024 * 1024;

const SellingUploadDocument = forwardRef<
  FileDropzoneRef,
  SellingUploadDocumentProps
>(({ onUploaded, isMultiple, disabled, config }, ref) => {
  const [files, setFiles] = useState<File[]>([]);
  const [errorMessage, setErrorMessage] = useState("");

  const allowedExtensions = [
    ".pdf",
    ".jpg",
    ".jpeg",
    ".png",
    ".csv",
    ".xlsx",
    ".xls",
    ".docx",
  ];

  const nameValidator = (file: File) => {
    const extension = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();

    if (!allowedExtensions.includes(extension)) {
      return {
        code: "file-invalid-type",
        message: `File type ${extension} is not supported.`,
      };
    }
    return null;
  };

  const onDrop = useCallback(
    async (acceptedFiles: File[]) => {
      const updatedFiles = [...files, ...acceptedFiles];

      const cleanedFiles = await Promise.all(updatedFiles.map(cleanFile));

      setFiles(cleanedFiles);
      onUploaded(cleanedFiles);
      setErrorMessage("");
    },
    [files, onUploaded],
  );

  const cleanFile = async (file: File): Promise<File> => {
    const extension = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();
    if (extension !== ".csv") {
      return file;
    }

    const text = await file.text();

    const cleanedText = text
      .split(/\r?\n/)
      .filter((line) => line.trim() !== "" && !/^,+$/.test(line.trim()))
      .join("\n");

    return new File([cleanedText], file.name, {
      type: "text/csv",
    });
  };

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
  const { getRootProps, getInputProps } = useDropzone({
    onDrop,
    onDropRejected: (rejectedFiles) => {
      // Fallback hook callback if you prefer handling errors on-drop
      handleErrors(rejectedFiles);
    },
    validator: nameValidator,
    multiple,
    accept: acceptedFiles as Accept,
    maxSize: SELLING_DOCUMENT_MAX_FILE_SIZE_BYTES,
    disabled,
  });

  const handleErrors = (rejections: FileRejection[]) => {
    rejections.forEach((rejection) => {
      rejection.errors.forEach((err) => {
        if (err.code === "file-too-large") {
          setErrorMessage(
            `This file is too large. Maximum size is ${SELLING_DOCUMENT_MAX_FILE_SIZE_MB} MB.`,
          );
        } else {
          setErrorMessage(err.message);
        }
      });
    });
  };

  useEffect(() => {}, [errorMessage]);

  return (
    <section>
      <div
        {...getRootProps()}
        className={`border-2 border-dashed rounded-xl p-8 text-center transition-colors ${
          disabled
            ? "border-gray-100 cursor-not-allowed"
            : "border-gray-200 cursor-pointer"
        }`}
      >
        <input {...getInputProps()} />

        <CloudUpload
          className={`h-6 w-6 mb-2 mx-auto ${disabled ? "text-gray-200" : "text-gray-300"}`}
        />
        <p className={`text-sm ${disabled ? "text-gray-400" : "text-gray-500"}`}>
          Click or drag file to upload
        </p>
        <p className={`text-xs mt-1 ${disabled ? "text-gray-300" : "text-gray-400"}`}>
          {acceptedLabel} (max {SELLING_DOCUMENT_MAX_FILE_SIZE_MB} MB)
        </p>
      </div>
      {errorMessage && (
        <p className="text-red-500 bg-red-100/80 rounded-md p-4 my-3 text-sm whitespace-pre-line break-all">
          <CircleAlert className="inline h-4 w-4" /> {errorMessage}
        </p>
      )}
    </section>
  );
});

export default SellingUploadDocument;
