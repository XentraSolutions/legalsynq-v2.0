# LegalSynq Mobile

React Native Expo app for the LegalSynq lien selling and buying workflow.

## App Identity

- Name: `LegalSynq`
- Version: `3.0.0`
- Expo SDK: `54`
- React Native: `0.81.5`
- React: `19.1.0`
- iOS bundle ID: `com.legalsynq`
- Android package: `com.legalsynq`

## Prerequisites

- Node.js `20.19.x` or newer. Expo SDK 54 requires at least Node `20.19.x`; the app has been validated locally with Node `26.0.0`.
- `pnpm` is the expected package manager. The root repo declares `pnpm@10.26.1`.
- Expo Go on a physical iOS or Android device, or local iOS/Android simulator tooling.
- Optional for cloud/native builds: Expo account and EAS CLI.

## Install Dependencies

From the repository root:

```bash
pnpm --dir apps/mobile install
```

Or from this directory:

```bash
pnpm install
```

The app has its own `pnpm-lock.yaml` and local `pnpm-workspace.yaml` because it is currently a standalone package under the monorepo.

## Environment

The mobile app reads public runtime configuration from:

```text
EXPO_PUBLIC_API_URL
EXPO_PUBLIC_APP_ENV
```

Default values:

```bash
EXPO_PUBLIC_API_URL=https://core-qa.legalsynq.net
```

Current EAS build values:

| Environment | `EXPO_PUBLIC_APP_ENV` | `EXPO_PUBLIC_API_URL`           |
| ----------- | --------------------- | ------------------------------- |
| QA          | `qa`                  | `https://core-qa.legalsynq.net` |
| Production  | `production`          | `https://core-qa.legalsynq.net` |

`EXPO_PUBLIC_*` values are bundled into the app binary and must not contain secrets.

## Verified HTTPS Deep Links

DL-APP-001 configures the application side of iOS Universal Links and Android App Links. Set the public, non-secret host for the active EAS environment:

```text
EXPO_PUBLIC_DEEP_LINK_HOST=links.approved-domain.example
```

The value must be a DNS hostname only: do not include `https://`, a port, path, query, or fragment. Do not infer this value from `EXPO_PUBLIC_API_URL`.

| EAS profile   | Logical app environment | App identity       | Host behavior                                                                        |
| ------------- | ----------------------- | ------------------ | ------------------------------------------------------------------------------------ |
| `development` | `development`           | `com.legalsynq.qa` | Uses its explicitly configured host; omits verified-link claims when unavailable.    |
| `preview`     | `qa`                    | `com.legalsynq.qa` | Uses its explicitly configured QA host; omits verified-link claims when unavailable. |
| `production`  | `production`            | `com.legalsynq`    | Requires a valid host and fails Expo config resolution when missing or invalid.      |

No UAT Mobile profile exists. A host never falls back to another environment.

When configured, Expo emits:

- iOS `com.apple.developer.associated-domains` with exactly `applinks:<host>`.
- Android HTTPS `VIEW` handling with `BROWSABLE`, `DEFAULT`, and `autoVerify=true`.

Android claims are derived directly from `shared/contracts/deep-links/routes.json`. `/dashboard` is exact; parameterized routes use the narrow prefixes `/deals/`, `/contacts/`, `/applications/`, and `/reports/`. Slash-terminated prefixes avoid claiming similarly named paths such as `/deals-marketing`, but they intentionally include descendants within each supported route family. iOS path restrictions belong in the AASA file.

The existing bundle/package identity logic and generated custom URL scheme remain unchanged. `DeepLinkingService` is not an incoming-link router; this configuration does not parse URLs or navigate.

### Inspect and validate configuration

Run focused checks from the repository root:

```bash
pnpm --dir apps/mobile exec jest --runInBand app.config.test.js src/shared/deepLinks/index.test.ts
pnpm --dir apps/mobile typecheck
pnpm --dir apps/mobile lint
```

Inspect a configured profile without writing native files:

```bash
EXPO_PUBLIC_APP_ENV=qa \
EXPO_PUBLIC_DEEP_LINK_HOST=<approved-qa-host> \
pnpm --dir apps/mobile exec expo config --type prebuild --json
```

Because `ios/` and `android/` are checked in, changing only `app.config.js` does not update committed native files. After approved hosts are available, generate and review native changes deliberately with the repository's Expo prebuild workflow before building. Never run a clean prebuild over uncommitted native work.

For troubleshooting:

- A Production `expo config` failure mentioning `EXPO_PUBLIC_DEEP_LINK_HOST` means the required environment value is absent or malformed.
- Empty Development/QA associated domains and intent filters mean no approved host was supplied for that build.
- Domain verification and device launch cannot succeed from app configuration alone. DL-BE-001 must serve a valid `apple-app-site-association` file and `assetlinks.json`, including the real Apple application/team data and Android signing fingerprints.
- This ticket does not configure DNS/TLS, association hosting, URL listeners, navigation, authentication continuation, resource lookup, or analytics.

## Deep-Link Routing Engine

DL-APP-002 adds a Mobile-only routing layer under `src/shared/services/DeepLinking`. Its boundary is deliberately limited:

```text
raw URL → validate/normalize → shared-route match → typed resolution
```

It does not navigate, inspect authentication, call an API, or persist a pending link.

The routing modules have separate responsibilities:

- `DeepLinkResolver` is pure. It validates HTTPS scheme/host, parses the path/query, matches enabled definitions from the authoritative `shared/contracts/deep-links/routes.json`, and returns a discriminated result.
- `DeepLinkDuplicateGuard` keeps normalized resolved URLs in memory for a two-second window. A repeat inside the window returns `duplicate`; different URLs and the same URL after expiry can proceed.
- `DeepLinkingService` remains the thin Expo adapter for initial URLs, custom URL creation, and runtime URL subscription/removal.
- `DeepLinkIntakeService` coordinates cold-start/runtime intake. It delivers resolved and failure results to a callback, but deliberately withholds duplicate results from that downstream callback.

Successful results use `status: "resolved"` and include `routeKey`, decoded `pathParameters`, approved `queryParameters`, `originalUrl`, and `normalizedUrl`. Expected failures use stable statuses: `malformed`, `unsupported_scheme`, `unsupported_host`, `unsupported_route`, `invalid_parameters`, or `duplicate`.

Routing behavior is intentionally strict:

- Only HTTPS is accepted by the resolver. Existing `DeepLinkingService.createUrl()` custom-scheme behavior remains unchanged, but custom schemes are not routed because no existing consumer requires them.
- The hostname must match the active `EXPO_PUBLIC_DEEP_LINK_HOST`, case-insensitively. If that environment has no configured host, HTTPS links return `unsupported_host`.
- Static and parameterized paths match by exact segment count. Trailing/duplicate slashes, dot segments, missing IDs, and extra segments are rejected.
- Path parameters are safely percent-decoded and canonically re-encoded in the normalized URL. The resolver performs no resource lookup or business validation.
- Fragments are rejected. Query parameters are decoded and checked against each shared route's `optionalQueryParameters`; because the current registry approves none, any query parameter is rejected.

Cold-start consumers call `processInitialUrl(callback)`. Runtime consumers call `subscribe(callback)` and must invoke the returned cleanup function. A future application integration should use one intake instance for both paths so the shared duplicate guard can suppress an initial URL repeated by a runtime event.

DL-APP-003 owns authentication and pending-route continuation outside this routing layer. DL-APP-004
owns mapping a resolved `routeKey` and parameters to React Navigation screens.

## Deep-Link Authentication Continuation

DL-APP-003 connects the typed DL-APP-002 output to the existing Mobile auth lifecycle without
performing navigation:

```text
DeepLinkResolution → auth gate → ready ResolvedDeepLink event
```

The app owns one configured `DeepLinkIntakeService` instance for both the initial URL and runtime
subscription, preserving APP-002's shared duplicate guard. Only `status: "resolved"` outcomes enter
the auth coordinator; malformed, unsupported, invalid, and duplicate outcomes are ignored by this
layer.

Authentication now has three explicit states. During `hydrating`, a resolved intent is held without
showing login or emitting it. If hydration resolves authenticated, the intent is cleared and emitted
once. If hydration resolves unauthenticated, it remains pending while the existing auth stack handles
login naturally. An intent received while already authenticated is emitted immediately and is never
stored.

Pending state is in memory only and holds a single intent. When another resolved intent arrives
before login, the latest intent replaces the earlier one. Successful password or biometric login
updates the existing `authAtom`; that transition clears the pending value before emitting it, so
repeated authenticated updates cannot replay it. Logout, unauthorized session clearing, tenant/API
mode changes, and authenticated identity changes clear pending state to prevent cross-session or
cross-user continuation.

`ReadyDeepLinkService.subscribe(callback)` is the navigation-independent APP-004 handoff. Its
callback receives only an authenticated `ResolvedDeepLink`, and the returned function removes the
listener. It does not buffer or persist events. APP-004 remains responsible for subscribing while its
consumer is mounted and navigation-ready, mapping route keys to screens, and performing actual
navigation. No React Navigation action, resource lookup, authorization, Backend call, or persistent
pending route is part of DL-APP-003.

Focused validation:

```bash
pnpm --dir apps/mobile exec jest --runInBand src/shared/services/DeepLinking
pnpm --dir apps/mobile typecheck
pnpm --dir apps/mobile lint
```

Dashboard demo data is controlled at runtime from `Settings > Reports > Use Dummy Dashboard Data`. The setting is stored locally on the device and does not require rebuilding the app.

Example local run against the gateway:

```bash
EXPO_PUBLIC_API_URL=http://localhost:5010/api pnpm --dir apps/mobile dev
```

Login always calls the configured authentication API. No local demo credentials or offline auth fallback are provided.

The configured auth login URL is `${EXPO_PUBLIC_API_URL}/auth/login`.

### Remembered Tenant Codes

The login flow stores tenant codes locally on the device after successful login or when a user adds a tenant code from `Switch Tenant`.

- Remembered tenants are app-local only. The app does not call tenant create, retrieve, list, or validation endpoints for this flow.
- Passwords are never stored.
- Tenant metadata is stored through Expo SecureStore and is retained after logout.
- Logout clears authentication/session state but keeps the active tenant so returning users only enter email and password.
- Adding or switching tenants clears the current authenticated session and requires the user to sign in again.

## Run The App

Start Expo:

```bash
pnpm --dir apps/mobile dev
```

Equivalent command:

```bash
pnpm --dir apps/mobile start
```

Expo will print a QR code and an `exp://...` URL. Open it with Expo Go or a simulator.

If the default Metro port is busy:

```bash
pnpm --dir apps/mobile dev -- --localhost --port 8082
```

## Run On Devices

After starting Expo:

- Press `i` for iOS simulator.
- Press `a` for Android emulator.
- Scan the QR code with Expo Go for a physical device.

For physical devices, make sure the device can reach the machine running Metro. If testing backend calls from a device, `localhost` points at the device, not your computer; use a LAN IP for `EXPO_PUBLIC_API_URL`.

## Quality Checks

Type-check:

```bash
pnpm --dir apps/mobile typecheck
```

Lint:

```bash
pnpm --dir apps/mobile lint
```

Run tests:

```bash
pnpm --dir apps/mobile test
```

Run coverage:

```bash
pnpm --dir apps/mobile exec jest --coverage --runInBand
```

Check Expo SDK dependency alignment:

```bash
pnpm --dir apps/mobile exec expo install --check
pnpm --dir apps/mobile peers check
(cd apps/mobile && pnpm dlx expo-doctor)
```

Storybook:

```bash
pnpm --dir apps/mobile storybook:start
```

## Build

This app is Expo-managed. For production native binaries, use EAS Build.

Install or use EAS CLI:

```bash
npm install -g eas-cli
```

Log in:

```bash
eas login
```

Configure EAS the first time:

```bash
cd apps/mobile
eas build:configure
```

Build Android with the default profile:

```bash
cd apps/mobile
eas build --platform android
```

Build iOS with the default profile:

```bash
cd apps/mobile
eas build --platform ios
```

Build both platforms with the default profile:

```bash
cd apps/mobile
eas build --platform all
```

Build QA iOS:

```bash
cd apps/mobile
eas build --platform ios --profile qa
```

Build production iOS:

```bash
cd apps/mobile
eas build --platform ios --profile production
```

## EAS Workflows

QA builds use `apps/mobile/.eas/workflows/create-qa-builds.yml`.

- Trigger: push to `main`
- Build profile: `qa`
- Submit profile: `qa`

Production builds use `apps/mobile/.eas/workflows/create-production-builds.yml`.

- Trigger: manual `workflow_dispatch`
- Build profile: `production`
- Submit profile: `production`

## Install A Build

### Expo Go Development Install

1. Run `pnpm --dir apps/mobile dev`.
2. Open Expo Go.
3. Scan the QR code.

### Android Build Install

After `eas build --platform android`, download the generated APK/AAB from Expo. For APK builds, install on a device with:

```bash
adb install path/to/app.apk
```

For Play Store distribution, upload the AAB through Google Play Console.

### iOS Build Install

After `eas build --platform ios`, install through TestFlight or an internal distribution profile, depending on the EAS build profile configured in `eas.json`.

## Useful Smoke Test

1. Open the app and confirm `LoginScreen` renders.
2. If no tenant is stored, enter tenant code, email, and password.
3. Sign out and confirm the app returns to login with the active tenant displayed and the tenant code field hidden.
4. Open `Switch Tenant`, add or select a tenant code, and confirm the app returns to login before authenticating.
5. Confirm the dashboard loads summary cards and recent activity after signing in.
6. Open `Market`, select a lien, and submit an offer.
7. Open `Offers` and confirm the sent offer appears.
8. Open `Cases`, select a case, and add a note.
9. Open `Profile` then `Settings`, toggle biometrics/theme.

## Known Notes

- Expo SDK dependencies are aligned to SDK 54. `apps/mobile/pnpm-workspace.yaml` includes local pnpm settings for Storybook's `valibot` peer and the Bottom Sheet/Reanimated peer range used by the SDK 54 stack.
- Splash screen configuration uses the `expo-splash-screen` config plugin.
- On case-insensitive macOS filesystems, Expo CLI may print `Using src/app as the root directory for Expo Router` because the blueprint directory is `src/App`. The app does not use `expo-router`.
- `.expo/`, `coverage/`, and `node_modules/` are generated local artifacts and are ignored.
