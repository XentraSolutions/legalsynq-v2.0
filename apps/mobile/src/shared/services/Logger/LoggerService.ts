import { ConfigService } from '@/shared/services/Config';

const REDACTED_KEYS = ['authorization', 'password', 'token', 'accessToken', 'refreshToken'];

function sanitize(value: unknown): unknown {
  if (!value || typeof value !== 'object') {
    return value;
  }

  if (Array.isArray(value)) {
    return value.map(sanitize);
  }

  return Object.entries(value as Record<string, unknown>).reduce<Record<string, unknown>>(
    (accumulator, [key, item]) => {
      accumulator[key] = REDACTED_KEYS.includes(key) ? '[redacted]' : sanitize(item);
      return accumulator;
    },
    {}
  );
}

function shouldLog(): boolean {
  return ConfigService.getEnvironment() !== 'production';
}

export const LoggerService = {
  log(message: string, context?: object): void {
    if (shouldLog()) {
      console.log(message, sanitize(context));
    }
  },

  warn(message: string, context?: object): void {
    if (shouldLog()) {
      console.warn(message, sanitize(context));
    }
  },

  error(message: string, error?: Error, context?: object): void {
    if (shouldLog()) {
      console.error(message, error, sanitize(context));
    }
  },
};
