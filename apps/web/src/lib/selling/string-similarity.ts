function bigrams(value: string): string[] {
  const normalized = value.toLowerCase().trim();
  if (normalized.length < 2) return normalized ? [normalized] : [];
  const grams: string[] = [];
  for (let i = 0; i < normalized.length - 1; i++) {
    grams.push(normalized.slice(i, i + 2));
  }
  return grams;
}

/**
 * Sørensen–Dice coefficient over character bigrams — a cheap, dependency-free
 * way to rank "close enough" name matches (typos, missing/extra suffixes
 * like "Inc"/"LLC", minor reordering) without a fuzzy-search backend. Returns
 * 0..1, where 1 is an exact match (case-insensitive).
 */
export function nameSimilarity(a: string, b: string): number {
  if (!a || !b) return 0;
  const ga = bigrams(a);
  const gb = bigrams(b);
  if (ga.length === 0 || gb.length === 0) return 0;

  const counts = new Map<string, number>();
  for (const g of ga) counts.set(g, (counts.get(g) ?? 0) + 1);

  let matches = 0;
  for (const g of gb) {
    const remaining = counts.get(g) ?? 0;
    if (remaining > 0) {
      matches++;
      counts.set(g, remaining - 1);
    }
  }

  return (2 * matches) / (ga.length + gb.length);
}
