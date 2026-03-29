import crypto from 'crypto';
import { DocumentRepository }   from '@/infrastructure/database/document-repository';
import { getStorageProvider }   from '@/infrastructure/storage/storage-factory';
import { auditService }         from './audit-service';
import { ScanService }          from './scan-service';
import { AccessTokenService }   from './access-token-service';
import { config }               from '@/shared/config';
import { NotFoundError, ForbiddenError, ScanBlockedError } from '@/shared/errors';
import { AuditEvent }           from '@/shared/constants';
import type { AuthPrincipal }   from '@/domain/interfaces/auth-provider';
import type { CreateDocumentInput, UpdateDocumentInput } from '@/domain/entities/document';
import type { IssuedToken, PresignedUrlResult } from '@/domain/entities/access-token';


interface RequestContext {
  principal:     AuthPrincipal;
  correlationId: string;
  ipAddress?:    string;
  userAgent?:    string;
}

function buildStorageKey(tenantId: string, documentId: string, filename: string): string {
  const ext = filename.split('.').pop() ?? 'bin';
  return `${tenantId}/${documentId}/${Date.now()}.${ext}`;
}

function sha256(buffer: Buffer): string {
  return crypto.createHash('sha256').update(buffer).digest('hex');
}

export const DocumentService = {
  // ── Create ─────────────────────────────────────────────────────────────────
  async create(
    input: Omit<CreateDocumentInput, 'uploadedBy'>,
    fileBuffer: Buffer,
    originalName: string,
    ctx: RequestContext,
  ) {
    const storage   = getStorageProvider();
    const bucket    = config.AWS_BUCKET_NAME ?? 'docs-local';
    const docId     = crypto.randomUUID();
    const key       = buildStorageKey(input.tenantId, docId, originalName);
    const checksum  = sha256(fileBuffer);

    // ── Phase 1: scan before upload ─────────────────────────────────────────
    // We scan first so infected files are NEVER written to storage.
    // Note: to switch to async scanning, move this after upload + DB write,
    // set scanStatus=PENDING, and process via a background worker.
    const scanResult = await ScanService.scanDocument(fileBuffer, {
      documentId:    docId,
      tenantId:      input.tenantId,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      filename:      originalName,
    });

    // Reject infected files immediately — do NOT upload
    ScanService.assertNotInfected(scanResult);

    // ── Phase 2: upload to storage ──────────────────────────────────────────
    await storage.upload({
      bucket,
      key,
      body:     fileBuffer,
      mimeType: input.mimeType ?? 'application/octet-stream',
    });

    // ── Phase 3: persist document with scan result ──────────────────────────
    const doc = await DocumentRepository.create({
      ...input,
      uploadedBy:      ctx.principal.userId,
      storageKey:      key,
      storageBucket:   bucket,
      mimeType:        input.mimeType ?? 'application/octet-stream',
      fileSizeBytes:   fileBuffer.byteLength,
      checksum,
      scanStatus:      scanResult.status,
      scanCompletedAt: scanResult.scannedAt,
      scanThreats:     scanResult.threats ?? [],
    });

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId:    doc.id,
      event:         AuditEvent.DOCUMENT_CREATED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      ipAddress:     ctx.ipAddress,
      userAgent:     ctx.userAgent,
      outcome:       'SUCCESS',
      detail: {
        title:         doc.title,
        mimeType:      doc.mimeType,
        fileSizeBytes: doc.fileSizeBytes,
        scanStatus:    doc.scanStatus,
      },
    });

    return doc;
  },

  // ── List ───────────────────────────────────────────────────────────────────
  async list(opts: {
    tenantId:      string;
    productId?:    string;
    referenceId?:  string;
    referenceType?: string;
    status?:       string;
    limit?:        number;
    offset?:       number;
  }) {
    return DocumentRepository.list(opts.tenantId, opts);
  },

  // ── Get by ID ──────────────────────────────────────────────────────────────
  async getById(id: string, ctx: RequestContext) {
    const doc = await DocumentRepository.findById(id, ctx.principal.tenantId);
    if (!doc) throw new NotFoundError('Document', id);
    return doc;
  },

  // ── Update metadata ────────────────────────────────────────────────────────
  async update(id: string, input: Omit<UpdateDocumentInput, 'updatedBy'>, ctx: RequestContext) {
    const doc = await DocumentRepository.findById(id, ctx.principal.tenantId);
    if (!doc) throw new NotFoundError('Document', id);

    const updated = await DocumentRepository.update(id, ctx.principal.tenantId, {
      ...input,
      updatedBy: ctx.principal.userId,
    });

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId:    id,
      event:         input.status ? AuditEvent.DOCUMENT_STATUS_CHANGED : AuditEvent.DOCUMENT_UPDATED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      outcome:       'SUCCESS',
      detail:        { changes: input },
    });

    return updated;
  },

  // ── Soft delete ────────────────────────────────────────────────────────────
  async delete(id: string, ctx: RequestContext) {
    const doc = await DocumentRepository.findById(id, ctx.principal.tenantId);
    if (!doc) throw new NotFoundError('Document', id);

    // Prevent deletion of documents on legal hold
    if (doc.legalHoldAt) {
      throw new ForbiddenError('Document is on legal hold and cannot be deleted');
    }

    await DocumentRepository.softDelete(id, ctx.principal.tenantId, ctx.principal.userId);

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId:    id,
      event:         AuditEvent.DOCUMENT_DELETED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      outcome:       'SUCCESS',
      detail:        { title: doc.title },
    });
  },

  // ── Upload new version ─────────────────────────────────────────────────────
  async uploadVersion(
    documentId: string,
    fileBuffer: Buffer,
    originalName: string,
    label: string | undefined,
    ctx: RequestContext,
  ) {
    const doc = await DocumentRepository.findById(documentId, ctx.principal.tenantId);
    if (!doc) throw new NotFoundError('Document', documentId);

    const storage  = getStorageProvider();
    const bucket   = doc.storageBucket;
    const key      = buildStorageKey(ctx.principal.tenantId, documentId, originalName);
    const checksum = sha256(fileBuffer);

    // ── Scan before upload ──────────────────────────────────────────────────
    // Scan result will be attached to the version row after it's created.
    // We pre-run the scan so we can reject infected files without writing to storage.
    const scanResult = await ScanService.scanDocument(fileBuffer, {
      documentId,
      tenantId:      ctx.principal.tenantId,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      filename:      originalName,
    });

    ScanService.assertNotInfected(scanResult);

    // ── Upload to storage ────────────────────────────────────────────────────
    await storage.upload({
      bucket,
      key,
      body:     fileBuffer,
      mimeType: doc.mimeType,
    });

    // ── Create version record with scan outcome ──────────────────────────────
    const version = await DocumentRepository.createVersion({
      documentId,
      tenantId:          ctx.principal.tenantId,
      uploadedBy:        ctx.principal.userId,
      label,
      storageKey:        key,
      storageBucket:     bucket,
      mimeType:          doc.mimeType,
      fileSizeBytes:     fileBuffer.byteLength,
      checksum,
      scanStatus:        scanResult.status,
      scanCompletedAt:   scanResult.scannedAt,
      scanDurationMs:    scanResult.scanDurationMs,
      scanThreats:       scanResult.threats ?? [],
      scanEngineVersion: scanResult.engineVersion,
    });

    // Mirror scan status to parent document
    await DocumentRepository.updateDocumentScanStatus(documentId, ctx.principal.tenantId, {
      scanStatus:      scanResult.status,
      scanCompletedAt: scanResult.scannedAt,
      scanThreats:     scanResult.threats ?? [],
    });

    await auditService.log({
      tenantId:          ctx.principal.tenantId,
      documentId,
      documentVersionId: version.id,
      event:             AuditEvent.VERSION_UPLOADED,
      actorId:           ctx.principal.userId,
      actorRoles:        ctx.principal.roles,
      correlationId:     ctx.correlationId,
      outcome:           'SUCCESS',
      detail: {
        versionNumber:  version.versionNumber,
        fileSizeBytes:  version.fileSizeBytes,
        scanStatus:     version.scanStatus,
      },
    });

    return version;
  },

  // ── Request access (replaces generateSignedUrl in new flow) ───────────────
  /**
   * Primary access entry point.
   *
   * When DIRECT_PRESIGN_ENABLED=false (default — secure mode):
   *   Issues a short-lived opaque access token.
   *   Client redeems it at GET /access/:token.
   *   Storage key is never exposed.
   *
   * When DIRECT_PRESIGN_ENABLED=true (legacy / compat mode):
   *   Generates a pre-signed storage URL directly (old behaviour).
   *   Suitable for trusted clients or storage-direct architectures.
   */
  async requestAccess(
    documentId: string,
    type: 'view' | 'download',
    ctx: RequestContext,
  ): Promise<IssuedToken | PresignedUrlResult> {
    if (config.DIRECT_PRESIGN_ENABLED) {
      return this.generateSignedUrl(documentId, type, ctx);
    }
    return AccessTokenService.issue(documentId, type, ctx);
  },

  // ── Signed URL (internal / legacy) ────────────────────────────────────────
  /**
   * Generates a direct pre-signed URL from storage.
   * Used internally by token redemption (short TTL = 30s) and
   * as a fallback when DIRECT_PRESIGN_ENABLED=true.
   *
   * NEVER call this from a route handler directly unless DIRECT_PRESIGN_ENABLED=true.
   * Use requestAccess() which enforces the configured access model.
   */
  async generateSignedUrl(
    documentId: string,
    type: 'view' | 'download',
    ctx: RequestContext,
  ): Promise<PresignedUrlResult> {
    const doc = await DocumentRepository.findById(documentId, ctx.principal.tenantId);
    if (!doc) throw new NotFoundError('Document', documentId);

    // ── Scan gate ────────────────────────────────────────────────────────────
    try {
      ScanService.enforceCleanScan(doc.scanStatus, {
        documentId,
        correlationId: ctx.correlationId,
      });
    } catch (err) {
      if (err instanceof ScanBlockedError) {
        await auditService.log({
          tenantId:      ctx.principal.tenantId,
          documentId,
          event:         AuditEvent.SCAN_ACCESS_DENIED,
          actorId:       ctx.principal.userId,
          actorRoles:    ctx.principal.roles,
          correlationId: ctx.correlationId,
          outcome:       'DENIED',
          detail: {
            reason:     'scan_blocked',
            scanStatus: doc.scanStatus,
            urlType:    type,
          },
        });
      }
      throw err;
    }

    const storage    = getStorageProvider();
    const expiresIn  = config.SIGNED_URL_EXPIRY_SECONDS;

    const url = await storage.generateSignedUrl({
      bucket:           doc.storageBucket,
      key:              doc.storageKey,
      expiresInSeconds: expiresIn,
      operation:        'GET',
    });

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId,
      event:         type === 'view' ? AuditEvent.VIEW_URL_GENERATED : AuditEvent.DOWNLOAD_URL_GENERATED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      outcome:       'SUCCESS',
      detail:        { type, expiresInSeconds: expiresIn, scanStatus: doc.scanStatus },
    });

    return { url, expiresInSeconds: expiresIn };
  },

  // ── Versions list ──────────────────────────────────────────────────────────
  async listVersions(documentId: string, ctx: RequestContext) {
    const doc = await DocumentRepository.findById(documentId, ctx.principal.tenantId);
    if (!doc) throw new NotFoundError('Document', documentId);
    return DocumentRepository.listVersions(documentId, ctx.principal.tenantId);
  },
};
