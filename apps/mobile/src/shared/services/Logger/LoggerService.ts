import { ConfigService } from '@/shared/services/Config';

const REDACTED_KEY_PARTS = ['authorization', 'cookie', 'password', 'secret', 'token'];

function shouldRedactKey(key: string): boolean {
  const normalizedKey = key.toLowerCase();
  return REDACTED_KEY_PARTS.some((part) => normalizedKey.includes(part));
}

function sanitize(value: unknown): unknown {
  if (!value || typeof value !== 'object') {
    return value;
  }

  if (Array.isArray(value)) {
    return value.map(sanitize);
  }

  return Object.entries(value as Record<string, unknown>).reduce<Record<string, unknown>>(
    (accumulator, [key, item]) => {
      accumulator[key] = shouldRedactKey(key) ? '[redacted]' : sanitize(item);
      return accumulator;
    },
    {}
  );
}

function shouldLog(): boolean {
  return ConfigService.getEnvironment() !== 'production';
}

function tron(): typeof import('reactotron-react-native').default | undefined {
  if (__DEV__) {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    return require('reactotron-react-native').default;
  }
  return undefined;
}

export const LoggerService = {
  log(message: string, context?: object): void {
    if (shouldLog()) {
      console.log(message, sanitize(context));
      tron()?.log?.(message, sanitize(context) as Record<string, unknown>);
    }
  },

  warn(message: string, context?: object): void {
    if (shouldLog()) {
      console.warn(message, sanitize(context));
      tron()?.warn?.({ message, context: sanitize(context) });
    }
  },

  error(message: string, error?: Error, context?: object): void {
    if (shouldLog()) {
      console.error(message, error, sanitize(context));
      tron()?.error?.(message, { error: error?.message, ...sanitize(context) as object });
    }
  },
};
