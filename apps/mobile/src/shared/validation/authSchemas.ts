import { z } from 'zod';

const credentialsSchema = z.object({
  email: z.string().trim().email('Enter a valid email address'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
});

export const tenantCodeSchema = z.object({
  tenantCode: z.string().trim().min(1, 'Tenant code is required'),
});

export const loginSchema = credentialsSchema.extend({
  tenantCode: tenantCodeSchema.shape.tenantCode,
  deviceInfo: z
    .object({
      platform: z.string().min(1),
      appVersion: z.string().min(1),
      osVersion: z.string().min(1),
      deviceDisplayName: z.string().min(1),
    })
    .optional(),
});

export const returningLoginSchema = credentialsSchema.extend({
  tenantCode: z.string().trim().optional(),
});

export const forgotPasswordSchema = z.object({
  email: z.string().trim().email('Enter a valid email address'),
});

export const resetPasswordSchema = z.object({
  token: z.string().trim().min(1, 'Reset token is required'),
  newPassword: z.string().min(8, 'Password must be at least 8 characters'),
});

export const changePasswordSchema = z.object({
  currentPassword: z.string().min(1, 'Current password is required'),
  newPassword: z.string().min(8, 'Password must be at least 8 characters'),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
export type ReturningLoginFormValues = z.infer<typeof returningLoginSchema>;
export type TenantCodeFormValues = z.infer<typeof tenantCodeSchema>;
export type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;
