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

Default value:

```bash
http://localhost:5010/api
```

Current EAS build values:

| Environment | `EXPO_PUBLIC_APP_ENV` | `EXPO_PUBLIC_API_URL` |
| --- | --- | --- |
| QA | `qa` | `https://core-qa.legalsynq.net/identity/api` |
| Production | `production` | `https://core-qa.legalsynq.net/identity/api` |

`EXPO_PUBLIC_*` values are bundled into the app binary and must not contain secrets.

Example local run against the gateway:

```bash
EXPO_PUBLIC_API_URL=http://localhost:5010/api pnpm --dir apps/mobile dev
```

Login always calls the configured authentication API. No local demo credentials or offline auth fallback are provided.

The configured auth login URL is `${EXPO_PUBLIC_API_URL}/auth/login`.

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
2. Sign in with the demo credentials.
3. Confirm the dashboard loads summary cards and recent activity.
4. Open `Market`, select a lien, and submit an offer.
5. Open `Offers` and confirm the sent offer appears.
6. Open `Cases`, select a case, and add a note.
7. Open `Profile` then `Settings`, toggle biometrics/theme.
8. Sign out and confirm the app returns to login.

## Known Notes

- Expo SDK dependencies are aligned to SDK 54. `apps/mobile/pnpm-workspace.yaml` includes local pnpm settings for Storybook's `valibot` peer and the Bottom Sheet/Reanimated peer range used by the SDK 54 stack.
- Splash screen configuration uses the `expo-splash-screen` config plugin.
- On case-insensitive macOS filesystems, Expo CLI may print `Using src/app as the root directory for Expo Router` because the blueprint directory is `src/App`. The app does not use `expo-router`.
- `.expo/`, `coverage/`, and `node_modules/` are generated local artifacts and are ignored.
