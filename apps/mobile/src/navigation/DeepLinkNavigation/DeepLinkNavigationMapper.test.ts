import type { ResolvedDeepLink } from '@/shared/services/DeepLinking';

import { mapDeepLinkToNavigation } from './DeepLinkNavigationMapper';

function intent(routeKey: string, pathParameters: Record<string, string> = {}): ResolvedDeepLink {
  return {
    status: 'resolved',
    routeKey,
    pathParameters,
    queryParameters: {},
    originalUrl: 'https://links.example.test/dashboard',
    normalizedUrl: 'https://links.example.test/dashboard',
  };
}

describe('mapDeepLinkToNavigation', () => {
  it('maps dashboard through the existing Main and Tabs hierarchy', () => {
    expect(mapDeepLinkToNavigation(intent('dashboard'))).toEqual({
      status: 'mapped',
      target: {
        name: 'Main',
        params: { screen: 'Tabs', params: { screen: 'Dashboard' } },
      },
    });
  });

  it('maps contactDetails to the existing ContactDetail params', () => {
    expect(mapDeepLinkToNavigation(intent('contactDetails', { contactId: 'contact-1' }))).toEqual({
      status: 'mapped',
      target: {
        name: 'Main',
        params: { screen: 'ContactDetail', params: { contactId: 'contact-1' } },
      },
    });
  });

  it('returns controlled destination gaps for valid unsupported detail routes', () => {
    const results = [
      mapDeepLinkToNavigation(intent('dealDetails', { dealId: 'deal-1' })),
      mapDeepLinkToNavigation(intent('reportDetails', { reportId: 'report-1' })),
    ];

    expect(results.map((result) => result.status)).toEqual([
      'destination_unavailable',
      'destination_unavailable',
    ]);
  });

  it('maps a valid application GUID to ApplicationDetail unchanged', () => {
    const applicationId = '01989abc-1234-7000-8000-123456789abc';

    expect(mapDeepLinkToNavigation(intent('applicationDetails', { applicationId }))).toEqual({
      status: 'mapped',
      target: {
        name: 'Main',
        params: { screen: 'ApplicationDetail', params: { applicationId } },
      },
    });
  });

  it('rejects a non-GUID applicationId without navigating', () => {
    expect(
      mapDeepLinkToNavigation(intent('applicationDetails', { applicationId: 'application-1' }))
    ).toMatchObject({ status: 'invalid_parameters', routeKey: 'applicationDetails' });
  });

  it('accepts the complete canonical .NET Guid shape without imposing a UUID version', () => {
    const applicationId = '00000000-0000-0000-0000-000000000000';

    expect(mapDeepLinkToNavigation(intent('applicationDetails', { applicationId }))).toMatchObject({
      status: 'mapped',
      target: { params: { screen: 'ApplicationDetail', params: { applicationId } } },
    });
  });

  it('rejects missing or blank required IDs without navigating', () => {
    const results = [
      mapDeepLinkToNavigation(intent('dealDetails')),
      mapDeepLinkToNavigation(intent('contactDetails', { contactId: '   ' })),
      mapDeepLinkToNavigation(intent('applicationDetails')),
      mapDeepLinkToNavigation(intent('reportDetails')),
    ];

    expect(results.map((result) => result.status)).toEqual([
      'invalid_parameters',
      'invalid_parameters',
      'invalid_parameters',
      'invalid_parameters',
    ]);
  });

  it('fails safely for an unknown runtime route key', () => {
    expect(mapDeepLinkToNavigation(intent('futureRoute'))).toMatchObject({
      status: 'unsupported_route',
      routeKey: 'futureRoute',
    });
  });

  it('fails safely for a malformed runtime path-parameter fixture', () => {
    const malformed = {
      ...intent('contactDetails'),
      pathParameters: { contactId: 42 },
    } as unknown as ResolvedDeepLink;

    expect(mapDeepLinkToNavigation(malformed)).toMatchObject({
      status: 'invalid_parameters',
      routeKey: 'contactDetails',
    });
  });
});
