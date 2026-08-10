export const CARECONNECT_MESSAGE_MAX_FILES = 10;
export const CARECONNECT_MESSAGE_MAX_FILE_SIZE_BYTES = 50 * 1024 * 1024;

export const CARECONNECT_MESSAGE_ALLOWED_TYPES = [
  'application/pdf',
  'image/jpeg',
  'image/png',
  'image/gif',
  'image/webp',
  'image/tiff',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'application/vnd.ms-powerpoint',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation',
  'text/plain',
  'text/csv',
];

export interface SelectedCareConnectMessageFile {
  id: string;
  file: File;
}

export function formatCareConnectAttachmentBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function makeSelectedCareConnectMessageFiles(
  incoming: File[],
  existingCount: number,
): { files: SelectedCareConnectMessageFile[]; error: string | null } {
  const accepted: SelectedCareConnectMessageFile[] = [];
  const errors: string[] = [];

  for (const file of incoming) {
    if (existingCount + accepted.length >= CARECONNECT_MESSAGE_MAX_FILES) {
      errors.push(`A message can include at most ${CARECONNECT_MESSAGE_MAX_FILES} attachments`);
      break;
    }

    if (!CARECONNECT_MESSAGE_ALLOWED_TYPES.includes(file.type)) {
      errors.push(`"${file.name}" is an unsupported file type`);
      continue;
    }

    if (file.size > CARECONNECT_MESSAGE_MAX_FILE_SIZE_BYTES) {
      errors.push(`"${file.name}" exceeds the 50 MB limit`);
      continue;
    }

    accepted.push({
      id: `${file.name}-${file.size}-${file.lastModified}-${Math.random()}`,
      file,
    });
  }

  return {
    files: accepted,
    error: errors.length > 0 ? errors.join('; ') : null,
  };
}

export function appendCareConnectMessageFiles(form: FormData, files: SelectedCareConnectMessageFile[]) {
  for (const selected of files) {
    form.append('files', selected.file, selected.file.name);
  }
}
