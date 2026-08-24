"use client";

import type { ReactNode } from "react";
import { FileImage, FileSpreadsheet, FileText, FileType2 } from "lucide-react";

export function fileIconFor(filename: string) {
  const ext = filename.split(".").pop()?.toLowerCase() ?? "";
  if (["jpg", "jpeg", "png", "gif", "webp"].includes(ext)) return FileImage;
  if (["xls", "xlsx", "csv"].includes(ext)) return FileSpreadsheet;
  if (["doc", "docx"].includes(ext)) return FileType2;
  return FileText;
}

export function fileExtLabel(filename: string): string {
  return filename.split(".").pop()?.toUpperCase() ?? "FILE";
}

export interface UploadedFileRowProps {
  title: ReactNode;
  subtitle?: ReactNode;
  timestamp?: string | null;
  actions?: ReactNode;
  /** Defaults to the FileText icon — pass `fileIconFor(fileName)` for a type-specific one. */
  icon?: ReturnType<typeof fileIconFor>;
}

// Shared row for a single uploaded (or uploading) document — used wherever a
// file needs to render as an icon + name/meta + optional timestamp + actions.
// The icon is always based on the file's extension and does not change while
// uploading, so callers should resolve it from the eventual file name (via
// `fileIconFor`) even before the upload completes.
export function UploadedFileRow({
  title,
  subtitle,
  timestamp,
  actions,
  icon,
}: UploadedFileRowProps) {
  const Icon = icon ?? FileText;

  return (
    <div className="flex items-center gap-3 border border-gray-200 rounded-lg px-4 py-3">
      <div className="w-10 h-10 rounded bg-gray-50 border border-gray-100 flex items-center justify-center shrink-0 text-gray-500">
        <Icon className="w-5 h-5" />
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-gray-800 truncate">{title}</p>
        {subtitle && (
          <p className="text-xs text-gray-400 truncate">{subtitle}</p>
        )}
      </div>
      {timestamp && (
        <p className="text-xs text-gray-400 shrink-0 whitespace-nowrap">
          {new Date(timestamp).toLocaleDateString()} ·{" "}
          {new Date(timestamp).toLocaleTimeString()}
        </p>
      )}
      {actions && <div className="flex items-center gap-2 shrink-0">{actions}</div>}
    </div>
  );
}
