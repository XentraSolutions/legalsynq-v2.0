import { File, Paths } from 'expo-file-system';
import { isAvailableAsync, shareAsync } from 'expo-sharing';

function payoffFilename(caseNumber: string): string {
  const safeCaseNumber = caseNumber.trim().replace(/[^a-zA-Z0-9._-]+/g, '-');
  return `payoff-quote-${safeCaseNumber || 'case'}.pdf`;
}

export const PayoffQuoteService = {
  async share(url: string, caseNumber: string): Promise<void> {
    if (!(await isAvailableAsync())) {
      throw new Error('File sharing is not available on this device.');
    }

    const destination = new File(Paths.cache, payoffFilename(caseNumber));
    try {
      const file = await File.downloadFileAsync(url, destination, { idempotent: true });
      await shareAsync(file.uri, {
        dialogTitle: 'Share payoff quote',
        mimeType: 'application/pdf',
        UTI: 'com.adobe.pdf',
      });
    } finally {
      if (destination.exists) destination.delete();
    }
  },
};
