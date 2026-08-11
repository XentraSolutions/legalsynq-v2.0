import { z } from 'zod';

import {
  changePasswordSchema,
  forgotPasswordSchema,
  loginSchema,
  returningLoginSchema,
  resetPasswordSchema,
  tenantCodeSchema,
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

export const loginUserResponseSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  email: z.string().email(),
  firstName: z.string(),
  lastName: z.string(),
  isActive: z.boolean(),
  roles: z.array(z.string()),
  organizationId: z.string().nullable().optional(),
  orgType: z.string().nullable().optional(),
  productRoles: z.array(z.string()).nullable().optional(),
  avatarDocumentId: z.string().nullable().optional(),
});

export const tenantSummarySchema = z.object({
  tenantId: z.string(),
  tenantCode: z.string(),
  tenantName: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  apiEndpoint: z.string().nullable().optional(),
});

export const loginResponseSchema = z.object({
  accessToken: z.string(),
  expiresAtUtc: z.string(),
  refreshToken: z.string().nullable().optional(),
  refreshTokenExpiresAtUtc: z.string().nullable().optional(),
  deviceSessionId: z.string().nullable().optional(),
  user: loginUserResponseSchema,
  tenants: z.array(tenantSummarySchema).nullable().optional(),
});

export const refreshSessionResponseSchema = z.object({
  accessToken: z.string(),
  accessTokenExpiresAtUtc: z.string(),
  refreshToken: z.string(),
  refreshTokenExpiresAtUtc: z.string(),
  deviceSessionId: z.string(),
});

export {
  changePasswordSchema,
  forgotPasswordSchema,
  loginSchema,
  returningLoginSchema,
  resetPasswordSchema,
  tenantCodeSchema,
};
