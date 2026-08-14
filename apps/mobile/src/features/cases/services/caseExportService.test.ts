jest.mock('expo-file-system', () => ({
  File: jest.fn(() => ({
    create: jest.fn(),
    delete: jest.fn(),
    exists: true,
    uri: 'file:///cache/cases.csv',
    write: jest.fn(),
  })),
  Paths: { cache: 'file:///cache/' },
}));

jest.mock('expo-sharing', () => ({
  isAvailableAsync: jest.fn().mockResolvedValue(true),
  shareAsync: jest.fn().mockResolvedValue(undefined),
}));

import { File } from 'expo-file-system';
import { shareAsync } from 'expo-sharing';

import { CaseExportService } from './caseExportService';

describe('CaseExportService', () => {
  beforeEach(() => jest.clearAllMocks());

  it('shares the decoded CSV and removes the cached file', async () => {
    await CaseExportService.share({
      base64: Buffer.from('CaseCode\nCASE-001').toString('base64'),
      export_format: 'csv',
      filename: '../Cases Export.csv',
    });

    const file = (
      File as unknown as {
        mock: {
          results: Array<{
            value: {
              create: ReturnType<typeof jest.fn>;
              delete: ReturnType<typeof jest.fn>;
              uri: string;
              write: ReturnType<typeof jest.fn>;
            };
          }>;
        };
      }
    ).mock.results[0].value;
    const shareCall = (
      shareAsync as unknown as {
        mock: { calls: Array<[string, { mimeType?: string }]> };
      }
    ).mock.calls[0];

    expect(file.create).toHaveBeenCalledWith({ overwrite: true });
    expect(file.write).toHaveBeenCalled();
    expect(shareCall[0]).toBe(file.uri);
    expect(shareCall[1].mimeType).toBe('text/csv');
    expect(file.delete).toHaveBeenCalled();
  });
});
