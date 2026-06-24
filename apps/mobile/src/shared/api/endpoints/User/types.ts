import type { z } from 'zod';

import type { updatePhoneRequestSchema } from './schemas';

export type UpdatePhoneRequest = z.infer<typeof updatePhoneRequestSchema>;
