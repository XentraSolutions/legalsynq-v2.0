export function getOfferedLiensEmptyStateCopy(hasFilters: boolean): {
  title: string;
  description: string;
} {
  return hasFilters
    ? {
        title: 'No results match your filters',
        description: 'Adjust search or status filters to broaden the result set.',
      }
    : {
        title: 'No offered liens yet',
        description: 'Offered liens will appear here after the buyer API returns offers.',
      };
}
