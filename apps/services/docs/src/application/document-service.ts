import crypto from 'crypto';
import { DocumentRepository }   from '@/infrastructure/database/document-repository';
import { getStorageProvider }   from '@/infrastructure/storage/storage-factory';
import { auditService }         from './audit-service';
import { config }               from '@/shared/config';
import { NotFoundError, ForbiddenError } from '@/shared/errors';
import { AuditEvent }           from '@/shared/constants';
import type { AuthPrincipal }   from '@/domain/interfaces/auth-provider';
import type { CreateDocumentInput, UpdateDocumentInput } from '@/domain/entities/document';


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

    // Upload to storage first
    await storage.upload({
      bucket,
      key,
      body:     fileBuffer,
      mimeType: input.mimeType ?? 'application/octet-stream',
    });

    const doc = await DocumentRepository.create({
      ...input,
      uploadedBy:    ctx.principal.userId,
      storageKey:    key,
      storageBucket: bucket,
      mimeType:      input.mimeType ?? 'application/octet-stream',
      fileSizeBytes: fileBuffer.byteLength,
      checksum,
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
      detail:        { title: doc.title, mimeType: doc.mimeType, fileSizeBytes: doc.fileSizeBytes },
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

    await storage.upload({
      bucket,
      key,
      body:     fileBuffer,
      mimeType: doc.mimeType,
    });

    const version = await DocumentRepository.createVersion({
      documentId,
      tenantId:      ctx.principal.tenantId,
      uploadedBy:    ctx.principal.userId,
      label,
      storageKey:    key,
      storageBucket: bucket,
      mimeType:      doc.mimeType,
      fileSizeBytes: fileBuffer.byteLength,
      checksum,
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
      detail:            { versionNumber: version.versionNumber, fileSizeBytes: version.fileSizeBytes },
    });

    return version;
  },

  // ── Signed URL (view / download) ───────────────────────────────────────────
  async generateSignedUrl(
    documentId: string,
    type: 'view' | 'download',
    ctx: RequestContext,
  ): Promise<{ url: string; expiresInSeconds: number }> {
    const doc = await DocumentRepository.findById(documentId, ctx.principal.tenantId);
    if (!doc) throw new NotFoundError('Document', documentId);

    const storage    = getStorageProvider();
    const expiresIn  = config.SIGNED_URL_EXPIRY_SECONDS;

    const url = await storage.generateSignedUrl({
      bucket:          doc.storageBucket,
      key:             doc.storageKey,
      expiresInSeconds: expiresIn,
      operation:       'GET',
    });

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId,
      event:         type === 'view' ? AuditEvent.VIEW_URL_GENERATED : AuditEvent.DOWNLOAD_URL_GENERATED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      outcome:       'SUCCESS',
      detail:        { type, expiresInSeconds: expiresIn },
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
