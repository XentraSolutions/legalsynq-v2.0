---
name: CC server-component bearer token
description: How Control Center server components get the raw JWT for backend API calls — PlatformSession has no token field.
---

CC Next.js server components need the raw JWT to call backend services (Xenia, Documents, etc.).
`PlatformSession` (from `getSession()` / `getServerSession()`) does NOT expose the raw token.

**How to apply:**

```tsx
import { cookies } from 'next/headers';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';

const jar = await cookies();
const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';
// then pass as: headers: { Authorization: `Bearer ${token}` }
```

**Why:** `platform_session` is an httpOnly cookie holding the raw JWT. `PlatformSession` is the decoded, validated session shape populated by calling GET /identity/api/auth/me — it omits the raw JWT intentionally. Route handlers in CC all use this cookie pattern (see `apps/control-center/src/app/api/profile/avatar/route.ts` as reference).

**How to check:** If you see `session?.token` or `session?.accessToken` in a CC server component, it is a bug — the token field does not exist on `PlatformSession`.
