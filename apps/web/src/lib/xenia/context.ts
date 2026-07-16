import type { XeniaLookupResult, XeniaMessageMetadata } from './types';

export interface XeniaContextShape {
  path: string;
  source: string;
  route: {
    product: string | null;
    section: string | null;
    entityType: string | null;
  };
  entity: {
    kind: string;
    id: string;
  } | null;
  query: Record<string, string>;
  filters: Record<string, string>;
  initialContext: Record<string, unknown>;
}

export function buildXeniaContext(
  pathname: string,
  searchParams: URLSearchParams | Iterable<[string, string]> | { entries(): IterableIterator<[string, string]> },
  source: string,
  initialContext?: Record<string, unknown>,
): XeniaContextShape {
  const entries = 'entries' in searchParams
    ? Array.from(searchParams.entries())
    : Array.from(searchParams);
  const query = Object.fromEntries(entries);
  const segments = pathname
    .split('/')
    .map(segment => segment.trim())
    .filter(Boolean);

  const product = segments[0] ?? null;
  const section = segments[1] ?? null;
  const detailId = segments[2] ?? null;

  let entity: XeniaContextShape['entity'] = null;
  let entityType: string | null = section;

  if (product === 'careconnect' && section === 'referrals' && detailId) {
    entity = { kind: 'referral', id: detailId };
    entityType = 'referral';
  } else if (product === 'careconnect' && section === 'providers' && detailId) {
    entity = { kind: 'provider', id: detailId };
    entityType = 'provider';
  } else if (product === 'careconnect' && section === 'referrals') {
    entityType = 'referral_queue';
  } else if (product === 'careconnect' && section === 'providers') {
    entityType = 'provider_directory';
  } else if (product === 'lien' && section === 'cases' && detailId && segments[3] === 'liens' && segments[4]) {
    entity = { kind: 'lien', id: segments[4] };
    entityType = 'lien';
  } else if (product === 'lien' && section === 'cases' && detailId) {
    entity = { kind: 'case', id: detailId };
    entityType = 'case';
  } else if (product === 'lien' && ['liens', 'my-liens', 'marketplace', 'portfolio'].includes(section ?? '') && detailId) {
    entity = { kind: 'lien', id: detailId };
    entityType = 'lien';
  } else if (product === 'lien' && section === 'cases') {
    entityType = 'case_queue';
  } else if (product === 'lien' && ['liens', 'my-liens', 'marketplace', 'portfolio'].includes(section ?? '')) {
    entityType = 'lien_queue';
  }

  const filters = Object.fromEntries(
    Object.entries(query).filter(([key]) => (
      key === 'search' ||
      key === 'status' ||
      key === 'providerId' ||
      key === 'providerName' ||
      key === 'referrerName' ||
      key === 'subjectName' ||
      key === 'clientName' ||
      key === 'caseId' ||
      key === 'caseNumber' ||
      key === 'lienType' ||
      key === 'createdFrom' ||
      key === 'createdTo'
    )),
  );

  return {
    path: pathname,
    source,
    route: {
      product,
      section,
      entityType,
    },
    entity,
    query,
    filters,
    initialContext: initialContext ?? {},
  };
}

export function serializeXeniaContext(context: XeniaContextShape): string {
  return JSON.stringify(context);
}

export function buildStarterPrompts(agentKey: string, context: XeniaContextShape): string[] {
  if (context.entity?.kind === 'referral') {
    return [
      'Summarize this referral',
      'Show this referral history',
      'What needs attention next on this referral?',
    ];
  }

  if (context.route.product === 'careconnect' && context.route.entityType === 'referral_queue') {
    return [
      'Summarize my referral queue',
      'Find referrals by client, provider, or referrer',
      'Which referrals need attention first?',
    ];
  }

  if (context.entity?.kind === 'provider') {
    return [
      'Summarize this provider',
      'Find referrals for this provider',
      'Show similar providers accepting referrals',
    ];
  }

  if (context.entity?.kind === 'lien') {
    return [
      'Summarize this lien',
      'Find the case for this lien',
      'What needs attention next on this lien?',
    ];
  }

  if (context.entity?.kind === 'case') {
    return [
      'Summarize this case',
      'Show liens linked to this case',
      'What needs attention next on this case?',
    ];
  }

  if (context.route.product === 'lien' && context.route.entityType === 'lien_queue') {
    return [
      'Summarize my lien queue',
      'Find liens by client, case, or status',
      'Which liens need attention first?',
    ];
  }

  if (context.route.product === 'lien' && context.route.entityType === 'case_queue') {
    return [
      'Find cases by client or case number',
      'Show cases with open liens',
      'Summarize my lien case queue',
    ];
  }

  if (context.route.product === 'lien' || agentKey === 'synqlien') {
    return [
      'Search liens by client, case, or status',
      'Summarize my lien queue',
    ];
  }

  if (context.route.product === 'careconnect' || agentKey === 'careconnect') {
    return [
      'Search referrals by client, provider, or referrer',
      'Summarize my referral queue',
    ];
  }

  return [
    'Summarize my current page context',
    'What can you help me look up here?',
    'What should I focus on next?',
  ];
}

export function parseXeniaMessageMetadata(metadataJson: string | null | undefined): XeniaMessageMetadata {
  if (!metadataJson) {
    return {
      lookupResults: [],
      followUpPrompts: [],
    };
  }

  try {
    const parsed = JSON.parse(metadataJson) as {
      lookupResults?: unknown[];
      followUpPrompts?: unknown[];
    };

    const lookupResults = Array.isArray(parsed.lookupResults)
      ? parsed.lookupResults
        .map(normalizeLookupResult)
        .filter((value): value is XeniaLookupResult => value !== null)
      : [];

    const followUpPrompts = Array.isArray(parsed.followUpPrompts)
      ? parsed.followUpPrompts
        .filter((value): value is string => typeof value === 'string' && value.trim().length > 0)
      : [];

    return {
      lookupResults,
      followUpPrompts,
    };
  } catch {
    return {
      lookupResults: [],
      followUpPrompts: [],
    };
  }
}

function normalizeLookupResult(value: unknown): XeniaLookupResult | null {
  if (!value || typeof value !== 'object') return null;

  const record = value as Record<string, unknown>;
  const kind = asString(record.kind);
  const id = asString(record.id);
  const title = asString(record.title);
  if (!kind || !id || !title) return null;

  return {
    kind,
    id,
    title,
    subtitle: asNullableString(record.subtitle),
    description: asNullableString(record.description),
    status: asNullableString(record.status),
    url: asNullableString(record.url),
    badges: Array.isArray(record.badges)
      ? record.badges.filter((badge): badge is string => typeof badge === 'string' && badge.trim().length > 0)
      : [],
  };
}

function asString(value: unknown): string | null {
  return typeof value === 'string' && value.trim().length > 0
    ? value.trim()
    : null;
}

function asNullableString(value: unknown): string | null {
  return typeof value === 'string' && value.trim().length > 0
    ? value.trim()
    : null;
}
