import type {
  RateLimitProvider,
  RateLimitKey,
  RateLimitResult,
} from '@/domain/interfaces/rate-limit-provider';

/**
 * RedisRateLimitProvider — distributed fixed-window rate limiting via Redis.
 *
 * Algorithm (atomic Lua script):
 *  1. INCR key → new count
 *  2. If count == 1, EXPIRE key windowSeconds (first hit initialises TTL)
 *  3. GET TTL for resetAt calculation
 *
 * Atomicity: Lua script runs as a single Redis command, preventing race conditions.
 *
 * To activate:
 *  1. Install ioredis: npm install ioredis && npm install -D @types/ioredis
 *  2. Set RATE_LIMIT_PROVIDER=redis and REDIS_URL in environment
 *  3. Uncomment the implementation below
 *
 * Key format: rl:{type}:{identifier}:{windowBucket}
 *   e.g.  rl:ip:192.168.1.1:1711900    (windowBucket = floor(now/windowSec))
 */
export class RedisRateLimitProvider implements RateLimitProvider {
  // private readonly redis: Redis;

  constructor() {
    // ── Activate by uncommenting: ──────────────────────────────────────────
    // const Redis = require('ioredis');
    // this.redis = new Redis(process.env['REDIS_URL'] ?? 'redis://localhost:6379', {
    //   maxRetriesPerRequest: 2,
    //   enableReadyCheck: true,
    //   lazyConnect: false,
    // });
    throw new Error(
      'RedisRateLimitProvider scaffold — install ioredis and uncomment the implementation.',
    );
  }

  async check(_key: RateLimitKey): Promise<RateLimitResult> {
    // ── Lua script for atomic INCR + conditional EXPIRE ───────────────────
    // const LUA = `
    //   local key = KEYS[1]
    //   local limit = tonumber(ARGV[1])
    //   local window = tonumber(ARGV[2])
    //   local count = redis.call('INCR', key)
    //   if count == 1 then
    //     redis.call('EXPIRE', key, window)
    //   end
    //   local ttl = redis.call('TTL', key)
    //   return {count, ttl}
    // `;
    //
    // const windowBucket = Math.floor(Date.now() / 1000 / _key.windowSeconds);
    // const redisKey = `rl:${_key.type}:${_key.identifier}:${windowBucket}`;
    //
    // const [count, ttl] = await this.redis.eval(LUA, 1, redisKey, _key.maxRequests, _key.windowSeconds) as [number, number];
    //
    // const now         = Date.now();
    // const resetAt     = now + ttl * 1000;
    // const remaining   = Math.max(0, _key.maxRequests - count);
    //
    // return {
    //   allowed:           count <= _key.maxRequests,
    //   remaining,
    //   limit:             _key.maxRequests,
    //   resetAt,
    //   retryAfterSeconds: ttl,
    // };

    throw new Error('Not implemented');
  }

  async reset(_type: string, _identifier: string): Promise<void> {
    // const pattern = `rl:${_type}:${_identifier}:*`;
    // const keys = await this.redis.keys(pattern);
    // if (keys.length > 0) await this.redis.del(...keys);
    throw new Error('Not implemented');
  }

  providerName(): string {
    return 'redis';
  }
}
