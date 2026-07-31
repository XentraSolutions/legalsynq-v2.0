# Typo-tolerant search design

## Scope

This is a design checkpoint only; the current change keeps Case and Lien search
semantics as exact substring matching. Any fuzzy search must explicitly cover
only these tenant-portal routes before it is added to assistant or reporting
surfaces:

- `GET /api/liens/liens` and `POST /api/liens/liens/search`: lien number,
  subject first/last name, and description.
- `GET /api/liens/cases`, `POST /api/liens/cases/v3`, and
  `POST /api/liens/cases/global-search`: case number and client first/last
  name; global search must retain its existing result categories.

## Proposed behavior

- Search names and free-text descriptions only; case and lien identifiers stay
  exact/prefix-oriented.
- Require at least three non-whitespace characters for a fuzzy fallback.
- Return exact/prefix matches first. Only when there are no exact matches may a
  one-edit/phonetic fallback be considered.
- Keep the existing filters, deterministic sort order, page size, and total
  count semantics. A fuzzy result must never bypass the tenant predicate or
  visibility/authorization filters.

## Implementation decision required

LiensDb uses MySQL, so no fallback may load an entire tenant's records into
memory to calculate edit distance. Before implementation, benchmark a
tenant-scoped indexed strategy (for example, generated normalized fields plus
FULLTEXT/phonetic support) against representative data. The selected strategy
must document its ranking behavior, p95 latency, result cap, and migration
plan. A database migration is expected if an index or generated search column
is required.

## Validation requirements

Add integration coverage for exact-match precedence, one-edit name typos,
active filters, stable paging, no cross-tenant results, and fallback behavior
at the minimum query length. Validate SQL and query plans against MySQL; the
in-memory API test provider cannot establish production search performance.
