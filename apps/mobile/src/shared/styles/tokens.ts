export const COLORS = {
  primary: '#2563eb',
  secondary: '#7c3aed',
  success: '#16a34a',
  warning: '#d97706',
  error: '#dc2626',
  info: '#0891b2',
  surface: '#ffffff',
  background: '#f8fafc',
  textPrimary: '#0f172a',
  textSecondary: '#475569',
  border: '#e2e8f0',
} as const;

export const RADII = {
  sm: 4,
  md: 8,
  lg: 12,
  xl: 16,
  '2xl': 20,
  full: 9999,
} as const;

export const SHADOWS = {
  sm: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 1,
  },
  md: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.1,
    shadowRadius: 6,
    elevation: 3,
  },
  lg: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.1,
    shadowRadius: 15,
    elevation: 5,
  },
} as const;

export const Z_INDEX = {
  dropdown: 10,
  sticky: 20,
  modal: 50,
  toast: 100,
  overlay: 200,
} as const;
