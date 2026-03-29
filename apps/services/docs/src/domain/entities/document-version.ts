/**
 * DocumentVersion entity — immutable once created.
 * Each upload of a new file creates a new version.
 */
export interface DocumentVersion {
  id:            string;
  documentId:    string;
  tenantId:      string;
  versionNumber: number;

  // Storage (internal only — never sent to clients)
  storageKey:    string;
  storageBucket: string;
  mimeType:      string;
  fileSizeBytes: number;
  checksum:      string;        // SHA-256

  // Virus scan
  scanStatus:    'PENDING' | 'CLEAN' | 'INFECTED' | 'SKIPPED';
  scanCompletedAt: Date | null;

  // Audit
  uploadedAt:    Date;
  uploadedBy:    string;
  label:         string | null; // e.g. "Final v1", "Redlined draft"
  isDeleted:     boolean;
  deletedAt:     Date | null;
  deletedBy:     string | null;
}

export interface CreateVersionInput {
  documentId:    string;
  tenantId:      string;
  uploadedBy:    string;
  label?:        string;
  storageKey:    string;
  storageBucket: string;
  mimeType:      string;
  fileSizeBytes: number;
  checksum:      string;
}
