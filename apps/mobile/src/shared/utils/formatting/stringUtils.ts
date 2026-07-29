export function titleCase(value: string): string {
  return value
    .toLowerCase()
    .split(/[\s_]+/)
    .filter(Boolean)
    .map((part) => `${part[0]?.toUpperCase() ?? ''}${part.slice(1)}`)
    .join(' ');
}

export function maskPatientName(value: string): string {
  const [firstName, lastName] = value.split(' ');
  const lastInitial = lastName?.[0] ? `${lastName[0].toUpperCase()}.` : '';
  return [firstName, lastInitial].filter(Boolean).join(' ');
}
