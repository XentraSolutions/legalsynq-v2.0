import { forwardRef, useCallback, useImperativeHandle, useState } from "react";
import { useDropzone } from "react-dropzone";

export interface UploadDocumentComponentProps {
  onUploaded: (files: File[]) => void;
}

export interface FileDropzoneRef {
  reset: () => void;
}

const UploadDocumentComponent = forwardRef<
  FileDropzoneRef,
  UploadDocumentComponentProps
>(({ onUploaded }, ref) => {
  const [files, setFiles] = useState<File[]>([]);

  const onDrop = useCallback(
    (acceptedFiles: File[]) => {
      const updatedFiles = [...files, ...acceptedFiles];

      setFiles(updatedFiles);
      onUploaded(updatedFiles);
    },
    [files, onUploaded],
  );

  useImperativeHandle(ref, () => ({
    reset() {
      setFiles([]);
      onUploaded([]);
    },
  }));

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    multiple: true,
    accept: {
      "application/pdf": [".pdf"],
      "image/*": [".jpg", ".jpeg", ".png"],
      "text/csv": [".csv"],
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [
        ".xlsx",
      ],
      "application/vnd.ms-excel": [".xls"],
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
        [".docx"],
    },
    maxSize: 10 * 1024 * 1024,
  });

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
        <p className="text-xs text-gray-400 mt-1">
          ".pdf,.jpg,.png,.docx,.xlsx,.xls,.csv" (max 10MB)
        </p>
      </div>

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
