import type { RateLimitProvider }      from '@/domain/interfaces/rate-limit-provider';
import { InMemoryRateLimitProvider }   from './in-memory-rate-limit-provider';
import { RedisRateLimitProvider }      from './redis-rate-limit-provider';
import { config }                      from '@/shared/config';
import { logger }                      from '@/shared/logger';

let _instance: RateLimitProvider | null = null;

export function getRateLimitProvider(): RateLimitProvider {
  if (_instance) return _instance;

  switch (config.RATE_LIMIT_PROVIDER) {
    case 'redis':
      _instance = new RedisRateLimitProvider();
      break;
    case 'memory':
    default:
      _instance = new InMemoryRateLimitProvider();
      break;
  }

  logger.info({ provider: _instance.providerName() }, 'Rate limit provider initialised');
  return _instance;
}
