import * as Sentry from '@sentry/react-native';

import { LoggerService } from '@/shared/services/Logger';

type ErrorTrackingContext = Record<string, unknown>;

const REDACTED_KEY_PARTS = [
  'authorization',
  'cookie',
  'password',
  'secret',
  'token',
];

function shouldRedactKey(key: string): boolean {
  const normalizedKey = key.toLowerCase();
  return REDACTED_KEY_PARTS.some((part) => normalizedKey.includes(part));
}

function sanitize(value: unknown, depth = 0): unknown {
  if (depth > 6) {
    return '[truncated]';
  }

  if (!value || typeof value !== 'object') {
    return value;
  }

  if (value instanceof Error) {
    return {
      message: value.message,
      name: value.name,
      stack: value.stack,
    };
  }

  if (Array.isArray(value)) {
    return value.slice(0, 50).map((item) => sanitize(item, depth + 1));
  }

  return Object.entries(value as ErrorTrackingContext).reduce<ErrorTrackingContext>(
    (accumulator, [key, item]) => {
      accumulator[key] = shouldRedactKey(key) ? '[redacted]' : sanitize(item, depth + 1);
      return accumulator;
    },
    {}
  );
}

function sanitizeContext(context?: ErrorTrackingContext): ErrorTrackingContext | undefined {
  if (!context) {
    return undefined;
  }

  const sanitized = sanitize(context);
  return sanitized && typeof sanitized === 'object' && !Array.isArray(sanitized)
    ? sanitized as ErrorTrackingContext
    : { value: sanitized };
}

export const ErrorTrackingService = {
  captureException(error: Error, context?: ErrorTrackingContext): void {
    const sanitizedContext = sanitizeContext(context);
    Sentry.captureException(error, sanitizedContext ? { extra: sanitizedContext } : undefined);
    LoggerService.error('Captured exception', error, sanitizedContext);
  },

  captureApiError(error: Error, context: ErrorTrackingContext): void {
    const sanitizedContext = sanitizeContext({ source: 'api', ...context });
    Sentry.addBreadcrumb({
      category: 'api',
      data: sanitizedContext,
      level: 'error',
      message: `${context.method ?? 'API'} ${context.url ?? 'request'} failed`,
    });

    this.captureException(error, sanitizedContext);
  },

  setCurrentScreen(name: string, params?: ErrorTrackingContext): void {
    const sanitizedParams = sanitizeContext(params);
    Sentry.setTag('screen', name);
    Sentry.setContext('screen', {
      name,
      params: sanitizedParams,
    });
    Sentry.addBreadcrumb({
      category: 'navigation',
      data: sanitizedParams,
      level: 'info',
      message: name,
    });
  },
};
