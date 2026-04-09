export function isIsoDateOnly(value: string): boolean {
  return /^\d{4}-\d{2}-\d{2}$/.test(value);
}

export function extractDateOnly(value: string | null | undefined): string {
  if (!value) {
    return '';
  }

  const match = value.match(/^(\d{4}-\d{2}-\d{2})/);
  return match?.[1] ?? '';
}

export function formatDateOnlyLabel(value: string | null | undefined): string {
  const normalized = extractDateOnly(value);

  if (!normalized) {
    return 'Nincs megadva';
  }

  const [year, month, day] = normalized.split('-');
  return `${year}.${month}.${day}.`;
}
