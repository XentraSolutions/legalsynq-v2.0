import 'dotenv/config';
import { z } from 'zod';

const schema = z.object({
  // Service
  NODE_ENV:        z.enum(['development', 'test', 'production']).default('development'),
  PORT:            z.coerce.number().default(5005),
  SERVICE_NAME:    z.string().default('docs-service'),
  LOG_LEVEL:       z.enum(['trace', 'debug', 'info', 'warn', 'error', 'fatal']).default('info'),

  // Database
  DATABASE_URL:    z.string().url(),

  // Storage
  STORAGE_PROVIDER: z.enum(['s3', 'local', 'gcs']).default('local'),
  LOCAL_STORAGE_PATH: z.string().default('./storage'),
  AWS_REGION:       z.string().optional(),
  AWS_BUCKET_NAME:  z.string().optional(),
  AWS_ACCESS_KEY_ID:     z.string().optional(),
  AWS_SECRET_ACCESS_KEY: z.string().optional(),
  S3_ENDPOINT_URL:  z.string().url().optional(),
  GCS_BUCKET_NAME:  z.string().optional(),
  GCS_PROJECT_ID:   z.string().optional(),
  GCS_KEY_FILE_PATH: z.string().optional(),

  // Auth
  AUTH_PROVIDER:    z.enum(['jwt', 'mock']).default('jwt'),
  JWT_ISSUER:       z.string().optional(),
  JWT_AUDIENCE:     z.string().optional(),
  JWT_JWKS_URI:     z.string().url().optional(),
  JWT_SECRET:       z.string().optional(),

  // Secrets
  SECRETS_PROVIDER: z.enum(['env', 'aws-sm', 'gcp-sm']).default('env'),

  // Signed URL
  SIGNED_URL_EXPIRY_SECONDS: z.coerce.number().default(300),

  // File limits
  MAX_FILE_SIZE_MB: z.coerce.number().default(50),

  // CORS
  CORS_ORIGINS: z.string().default('http://localhost:5000'),
});

function parseConfig() {
  const result = schema.safeParse(process.env);
  if (!result.success) {
    const issues = result.error.issues.map(i => `  ${i.path.join('.')}: ${i.message}`).join('\n');
    throw new Error(`[docs-service] Invalid configuration:\n${issues}`);
  }
  return result.data;
}

export const config = parseConfig();
export type Config = typeof config;
