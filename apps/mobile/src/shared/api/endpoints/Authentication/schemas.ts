import { z } from 'zod';

import {
  changePasswordSchema,
  forgotPasswordSchema,
  loginSchema,
  resetPasswordSchema,
} from '@/shared/validation/authSchemas';

export const userSessionSchema = z.object({
  id: z.string(),
  email: z.string().email(),
  firstName: z.string(),
  lastName: z.string(),
  roles: z.array(z.string()),
  permissions: z.array(z.string()),
  organization: z.object({
    id: z.string(),
    name: z.string(),
    tenantId: z.string(),
  }),
  tenantId: z.string(),
});

export const sessionEnvelopeSchema = z.object({
  user: userSessionSchema,
  issuedAt: z.string(),
  expiresAt: z.string(),
  tenantId: z.string(),
});

export const loginResponseSchema = z.object({
  accessToken: z.string(),
  sessionEnvelope: sessionEnvelopeSchema,
});

export {
  changePasswordSchema,
  forgotPasswordSchema,
  loginSchema,
  resetPasswordSchema,
};
