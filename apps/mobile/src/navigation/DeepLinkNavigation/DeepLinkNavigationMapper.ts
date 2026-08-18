import type { RootStackParamList } from '@/navigation/types/navigation';
import type { ResolvedDeepLink } from '@/shared/services/DeepLinking';

export interface DeepLinkNavigationTarget {
  name: 'Main';
  params: NonNullable<RootStackParamList['Main']>;
}

export type DeepLinkNavigationMappingResult =
  | { status: 'mapped'; target: DeepLinkNavigationTarget }
  | {
      status: 'unsupported_route' | 'destination_unavailable' | 'invalid_parameters';
      routeKey: string;
      reason: string;
    };

function requiredParameter(intent: ResolvedDeepLink, name: string): string | null {
  const value = intent.pathParameters[name];
  return typeof value === 'string' && value.trim() ? value : null;
}

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value);
}

export function mapDeepLinkToNavigation(intent: ResolvedDeepLink): DeepLinkNavigationMappingResult {
  switch (intent.routeKey) {
    case 'dashboard':
      return {
        status: 'mapped',
        target: {
          name: 'Main',
          params: { screen: 'Tabs', params: { screen: 'Dashboard' } },
        },
      };
    case 'contactDetails': {
      const contactId = requiredParameter(intent, 'contactId');
      if (!contactId) {
        return {
          status: 'invalid_parameters',
          routeKey: intent.routeKey,
          reason: 'Contact Details navigation requires contactId.',
        };
      }

      return {
        status: 'mapped',
        target: {
          name: 'Main',
          params: { screen: 'ContactDetail', params: { contactId } },
        },
      };
    }
    case 'dealDetails':
      if (!requiredParameter(intent, 'dealId')) {
        return {
          status: 'invalid_parameters',
          routeKey: intent.routeKey,
          reason: 'Deal Details navigation requires dealId.',
        };
      }
      return {
        status: 'destination_unavailable',
        routeKey: intent.routeKey,
        reason: `Mobile has no existing destination for '${intent.routeKey}'.`,
      };
    case 'applicationDetails': {
      const applicationId = requiredParameter(intent, 'applicationId');
      if (!applicationId || !isGuid(applicationId)) {
        return {
          status: 'invalid_parameters',
          routeKey: intent.routeKey,
          reason: 'Application Details navigation requires a valid applicationId GUID.',
        };
      }
      return {
        status: 'mapped',
        target: {
          name: 'Main',
          params: { screen: 'ApplicationDetail', params: { applicationId } },
        },
      };
    }
    case 'reportDetails':
      if (!requiredParameter(intent, 'reportId')) {
        return {
          status: 'invalid_parameters',
          routeKey: intent.routeKey,
          reason: 'Report Details navigation requires reportId.',
        };
      }
      return {
        status: 'destination_unavailable',
        routeKey: intent.routeKey,
        reason: `Mobile has no existing destination for '${intent.routeKey}'.`,
      };
    default:
      return {
        status: 'unsupported_route',
        routeKey: intent.routeKey,
        reason: `Mobile has no deep-link navigation mapping for '${intent.routeKey}'.`,
      };
  }
}
