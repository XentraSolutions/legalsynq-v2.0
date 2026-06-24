import type { z } from 'zod';

import type {
  changePasswordSchema,
  forgotPasswordSchema,
  loginResponseSchema,
  loginSchema,
  resetPasswordSchema,
  sessionEnvelopeSchema,
  userSessionSchema,
} from './schemas';

export type LoginRequest = z.infer<typeof loginSchema>;
export type LoginResponse = z.infer<typeof loginResponseSchema>;
export type ForgotPasswordRequest = z.infer<typeof forgotPasswordSchema>;
export type ResetPasswordRequest = z.infer<typeof resetPasswordSchema>;
export type ChangePasswordRequest = z.infer<typeof changePasswordSchema>;
export type SessionEnvelope = z.infer<typeof sessionEnvelopeSchema>;
export type UserSession = z.infer<typeof userSessionSchema>;
