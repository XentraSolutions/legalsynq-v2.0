const USD_FORMATTER = new Intl.NumberFormat('en-US', {
  currency: 'USD',
  maximumFractionDigits: 0,
  style: 'currency',
});

export function formatCurrency(value: number): string {
  return USD_FORMATTER.format(value);
}
