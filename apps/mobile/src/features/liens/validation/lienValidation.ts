import { z } from 'zod';

export const sellLienSchema = z.object({
  patientFirstName: z.string().trim().min(1, 'Patient first name is required'),
  patientLastName: z.string().trim().min(1, 'Patient last name is required'),
  caseType: z.enum(['AUTO_ACCIDENT', 'WORKERS_COMP', 'PERSONAL_INJURY', 'MEDICAL_MALPRACTICE']),
  incidentDate: z.string().trim().min(1, 'Incident date is required'),
  jurisdiction: z.string().trim().min(1, 'Jurisdiction is required'),
  caseReference: z.string().optional(),
  lienAmount: z.string().trim().min(1, 'Lien amount is required'),
  askingPrice: z.string().trim().min(1, 'Asking price is required'),
  notes: z.string().optional(),
});

export type SellLienFormValues = z.infer<typeof sellLienSchema>;
