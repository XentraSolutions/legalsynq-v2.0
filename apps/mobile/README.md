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
