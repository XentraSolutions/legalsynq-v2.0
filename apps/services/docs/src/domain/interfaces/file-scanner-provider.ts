/**
 * FileScannerProvider — pluggable malware / AV scanning hook.
 * Scaffold only — implementations connect to ClamAV, AWS GuardDuty Malware,
 * or GCP Security Command Center.
 */
export type ScanResult = {
  status:    'CLEAN' | 'INFECTED' | 'ERROR';
  threats?:  string[];
  scannedAt: Date;
};

export interface FileScannerProvider {
  /** Scan a buffer for malware. Returns result with status. */
  scan(buffer: Buffer, filename: string): Promise<ScanResult>;

  /** Return the name of the active scanner. */
  providerName(): string;
}
