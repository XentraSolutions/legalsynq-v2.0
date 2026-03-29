/**
 * Centralised error hierarchy for the Docs Service.
 * All errors carry an HTTP status code and a machine-readable code for the client.
 */

export class DocsError extends Error {
  constructor(
    message: string,
    public readonly statusCode: number,
    public readonly code: string,
    public readonly details?: unknown,
  ) {
    super(message);
    this.name = this.constructor.name;
    Error.captureStackTrace(this, this.constructor);
  }
}

// ── 400 Bad Request ────────────────────────────────────────────────────────────
export class ValidationError extends DocsError {
  constructor(message: string, details?: unknown) {
    super(message, 400, 'VALIDATION_ERROR', details);
  }
}

export class FileValidationError extends DocsError {
  constructor(message: string) {
    super(message, 400, 'FILE_VALIDATION_ERROR');
  }
}

// ── 401 Unauthorized ──────────────────────────────────────────────────────────
export class AuthenticationError extends DocsError {
  constructor(message = 'Authentication required') {
    super(message, 401, 'AUTHENTICATION_REQUIRED');
  }
}

// ── 403 Forbidden ─────────────────────────────────────────────────────────────
export class ForbiddenError extends DocsError {
  constructor(message = 'Access denied') {
    super(message, 403, 'ACCESS_DENIED');
  }
}

// ── 404 Not Found ─────────────────────────────────────────────────────────────
export class NotFoundError extends DocsError {
  constructor(resource: string, id: string) {
    super(`${resource} not found: ${id}`, 404, 'NOT_FOUND');
  }
}

// ── 409 Conflict ──────────────────────────────────────────────────────────────
export class ConflictError extends DocsError {
  constructor(message: string) {
    super(message, 409, 'CONFLICT');
  }
}

// ── 413 Payload Too Large ─────────────────────────────────────────────────────
export class FileTooLargeError extends DocsError {
  constructor(maxMb: number) {
    super(`File exceeds maximum allowed size of ${maxMb} MB`, 413, 'FILE_TOO_LARGE');
  }
}

// ── 422 Unprocessable Entity ──────────────────────────────────────────────────
export class UnsupportedFileTypeError extends DocsError {
  constructor(mimeType: string) {
    super(`File type not permitted: ${mimeType}`, 422, 'UNSUPPORTED_FILE_TYPE');
  }
}

// ── 403 Scan-blocked ─────────────────────────────────────────────────────────
export class ScanBlockedError extends DocsError {
  constructor(scanStatus: string) {
    super(
      `Access denied: file scan status is ${scanStatus}. Only CLEAN files may be accessed.`,
      403,
      'SCAN_BLOCKED',
    );
  }
}

export class InfectedFileError extends DocsError {
  constructor(threats: string[]) {
    super(
      `File rejected: malware detected (${threats.join(', ')})`,
      422,
      'INFECTED_FILE',
    );
  }
}

// ── 429 Too Many Requests ─────────────────────────────────────────────────────
export class RateLimitError extends DocsError {
  constructor(
    public readonly retryAfterSeconds: number,
    public readonly limitDimension: 'ip' | 'user' | 'tenant',
  ) {
    super(
      `Rate limit exceeded. Retry after ${retryAfterSeconds} second(s).`,
      429,
      'RATE_LIMIT_EXCEEDED',
    );
  }
}

// ── 500 Internal ──────────────────────────────────────────────────────────────
export class StorageError extends DocsError {
  constructor(message: string) {
    super(message, 500, 'STORAGE_ERROR');
  }
}

export class DatabaseError extends DocsError {
  constructor(message: string) {
    super(message, 500, 'DATABASE_ERROR');
  }
}
