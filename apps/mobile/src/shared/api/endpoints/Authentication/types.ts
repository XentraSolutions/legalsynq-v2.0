import type { z } from 'zod';

import type {
  changePasswordSchema,
  forgotPasswordSchema,
  loginUserResponseSchema,
  loginResponseSchema,
  loginSchema,
  refreshSessionResponseSchema,
  resetPasswordSchema,
  tenantSummarySchema,
  userSessionSchema,
} from './schemas';

export type LoginRequest = z.infer<typeof loginSchema>;
export type LoginResponse = z.infer<typeof loginResponseSchema>;
export type RefreshSessionResponse = z.infer<typeof refreshSessionResponseSchema>;
export type ForgotPasswordRequest = z.infer<typeof forgotPasswordSchema>;
export type ResetPasswordRequest = z.infer<typeof resetPasswordSchema>;
export type ChangePasswordRequest = z.infer<typeof changePasswordSchema>;
export type LoginUserResponse = z.infer<typeof loginUserResponseSchema>;
export type TenantSummary = z.infer<typeof tenantSummarySchema>;
export type UserSession = z.infer<typeof userSessionSchema>;
