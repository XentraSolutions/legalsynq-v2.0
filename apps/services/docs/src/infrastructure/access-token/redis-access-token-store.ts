import type { AccessTokenStore }  from '@/domain/interfaces/access-token-store';
import type { AccessToken }       from '@/domain/entities/access-token';
import { logger }                 from '@/shared/logger';
import { config }                 from '@/shared/config';

/**
 * RedisAccessTokenStore — distributed access-token storage scaffold.
 *
 * Uses Redis SETEX for atomic TTL management.
 * Uses Redis Lua script for atomic markUsed() to prevent TOCTOU races.
 *
 * Key schema:
 *   access_token:{tokenString}       → JSON(AccessToken), TTL = ACCESS_TOKEN_TTL_SECONDS
 *
 * To activate:
 *   1. Install ioredis: `npm install ioredis`
 *   2. Set REDIS_URL and ACCESS_TOKEN_STORE=redis
 *   3. Uncomment the implementation below
 *
 * Atomic markUsed() via Lua:
 *   The Lua script reads isUsed, sets it true, and returns 0 (already used) or 1 (success)
 *   atomically. This prevents two concurrent redemption requests both succeeding.
 */

// ── Lua script for atomic one-time-use enforcement ─────────────────────────────
// const MARK_USED_LUA = `
//   local key = KEYS[1]
//   local raw = redis.call('GET', key)
//   if not raw then return -1 end
//   local token = cjson.decode(raw)
//   if token.isUsed then return 0 end
//   token.isUsed = true
//   local ttl = redis.call('TTL', key)
//   redis.call('SET', key, cjson.encode(token), 'EX', ttl)
//   return 1
// `;

export class RedisAccessTokenStore implements AccessTokenStore {
  // private readonly redis: Redis;

  constructor() {
    logger.info(
      { redisUrl: config.REDIS_URL ? '[redacted]' : 'not set' },
      'RedisAccessTokenStore initialising (scaffold)',
    );
    // Uncomment when activating:
    // const Redis = (await import('ioredis')).default;
    // this.redis = new Redis(config.REDIS_URL!);
  }

  async store(_token: AccessToken): Promise<void> {
    // const ttl = Math.max(1, Math.ceil((_token.expiresAt.getTime() - Date.now()) / 1000));
    // const key  = `access_token:${_token.token}`;
    // await this.redis.setex(key, ttl, JSON.stringify(_token));
    logger.warn('RedisAccessTokenStore.store() — scaffold, falling back is not automatic');
    throw new Error('RedisAccessTokenStore is a scaffold. Set ACCESS_TOKEN_STORE=memory for dev.');
  }

  async get(tokenString: string): Promise<AccessToken | null> {
    // const raw = await this.redis.get(`access_token:${tokenString}`);
    // if (!raw) return null;
    // const token = JSON.parse(raw) as AccessToken;
    // token.expiresAt = new Date(token.expiresAt);
    // token.createdAt = new Date(token.createdAt);
    // return token;
    void tokenString;
    throw new Error('RedisAccessTokenStore is a scaffold.');
  }

  async markUsed(tokenString: string): Promise<boolean> {
    // const result = await this.redis.eval(MARK_USED_LUA, 1, `access_token:${tokenString}`);
    // return result === 1;
    void tokenString;
    throw new Error('RedisAccessTokenStore is a scaffold.');
  }

  async revoke(tokenString: string): Promise<void> {
    // await this.redis.del(`access_token:${tokenString}`);
    void tokenString;
    throw new Error('RedisAccessTokenStore is a scaffold.');
  }

  async cleanup(): Promise<number> {
    // Redis TTL handles expiry automatically — no manual cleanup needed
    return 0;
  }

  destroy(): void {
    // await this.redis.quit();
    logger.info('RedisAccessTokenStore destroyed (scaffold)');
  }
}
