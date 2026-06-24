/** Returns true when value is a valid 5-digit or ZIP+4 US postal code. */
export function isValidUsZipCode(value: string): boolean {
  return /^\d{5}(-\d{4})?$/.test(value.trim());
}
