/** Returns true when value is a valid 5-digit or ZIP+4 US postal code. */
export function isValidUsZipCode(value: string): boolean {
  return /^\d{5}(-\d{4})?$/.test(value.trim());
}

/**
 * Format a value as the user types a US ZIP code.
 * Extracts digits then applies progressive masking:
 *   1–5  → XXXXX
 *   6–9  → XXXXX-XXXX
 * Trims at 9 digits.
 */
export function formatZipInput(value: string): string {
  const digits = value.replace(/\D/g, "").slice(0, 9);
  if (digits.length <= 5) return digits;
  return `${digits.slice(0, 5)}-${digits.slice(5)}`;
}
