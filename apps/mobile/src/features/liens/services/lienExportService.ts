import { Buffer } from 'buffer';
import { File, Paths } from 'expo-file-system';
import { isAvailableAsync, shareAsync } from 'expo-sharing';

import type { LienExportFile } from '@/shared/api/endpoints/Liens';

function safeFilename(filename: string): string {
  const normalized = filename.trim().replace(/[^a-zA-Z0-9._-]+/g, '-');
  return normalized.toLowerCase().endsWith('.csv') ? normalized : `${normalized || 'liens'}.csv`;
}

export const LienExportService = {
  async share(filePayload: LienExportFile): Promise<void> {
    if (!(await isAvailableAsync())) {
      throw new Error('File sharing is not available on this device.');
    }

    const file = new File(Paths.cache, safeFilename(filePayload.filename));
    try {
      file.create({ overwrite: true });
      file.write(Uint8Array.from(Buffer.from(filePayload.base64, 'base64')));
      await shareAsync(file.uri, {
        dialogTitle: 'Export liens',
        mimeType: 'text/csv',
        UTI: 'public.comma-separated-values-text',
      });
    } finally {
      if (file.exists) file.delete();
    }
  },
};
