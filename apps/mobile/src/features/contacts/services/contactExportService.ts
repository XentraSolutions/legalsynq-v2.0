import * as FileSystem from 'expo-file-system/legacy';
import * as Sharing from 'expo-sharing';

export async function shareContactsCsv(base64: string, label: string): Promise<void> {
  const directory = FileSystem.cacheDirectory;
  if (!directory) throw new Error('Temporary storage is unavailable.');
  const file = `${directory}LegalSynq-${label.replace(/\s+/g, '-')}-Contacts.csv`;
  await FileSystem.writeAsStringAsync(file, base64, { encoding: FileSystem.EncodingType.Base64 });
  if (!(await Sharing.isAvailableAsync()))
    throw new Error('Sharing is unavailable on this device.');
  await Sharing.shareAsync(file, { mimeType: 'text/csv', dialogTitle: 'Export Contacts' });
}
