# Mobile App – Build Fix Changelog

**Branch:** `chore/mobile-app-initialize`
**Date:** 2026-06-25

---

## Summary

Fixed a cascade of build and runtime errors that prevented the iOS app from bundling, launching, and displaying correctly. All issues traced to dependency version mismatches introduced during initial mobile project setup.

---

## Changes

### 1. Removed `@babel/plugin-transform-react-jsx@^8.0.1` from `dependencies`

**Problem:** The Babel 8 version of this plugin pulled in `@babel/types@8.0.0`. The `react-native-worklets@0.5.1` Babel plugin used `t.numericLiteral(-27)` internally — valid in `@babel/types@7.x` but rejected in `8.0.0`. This caused a hard Metro bundler crash before any JS was emitted:

```
WorkletsBabelPluginError: NumericLiterals must be non-negative finite numbers.
```

**Fix:** Removed from `dependencies`. The plugin is not needed directly — `babel-preset-expo` already includes JSX transformation via `@react-native/babel-preset`.

---

### 2. Added `@babel/plugin-transform-react-jsx@^7.25.0` to `devDependencies`

**Problem:** After removing the plugin entirely, EAS CI builds failed with:

```
[BABEL] index.js: Cannot find module '@babel/plugin-transform-react-jsx'
```

EAS CI installs from the mobile app directory without the full monorepo pnpm virtual store context, so the transitive copy inside `@react-native/babel-preset`'s isolated `.pnpm` directory was not reachable.

**Fix:** Re-added as a `devDependency` pinned to `^7.25.0` (resolves to `7.29.7`). This makes it a first-class symlink at `node_modules/@babel/plugin-transform-react-jsx` — always discoverable. Version `7.x` requires `@babel/types@^7.x` only, so the Babel 8 conflict cannot recur.

---

### 3. Added `"@babel/types": "^7.29.0"` override in `pnpm-workspace.yaml`

**Problem:** Without an explicit override, any future dependency could silently re-introduce `@babel/types@8.x` and break the worklets Babel plugin again.

**Fix:** Added a workspace-level override pinning `@babel/types` to `^7.29.0`, preventing Babel 8 types from being hoisted regardless of transitive dependency changes.

---

### 4. Downgraded `babel-preset-expo` from `^56.0.15` → `~54.0.11`

**Problem:** After fixing the bundler crash, the app launched but immediately crashed at runtime:

```
[runtime not ready]: SyntaxError: private properties are not supported
```

`babel-preset-expo@56` is designed for Expo SDK 56 (React Native 0.82+). Its `hermes-v1` transform profile intentionally skips transforming private class fields (`#field` syntax), assuming Hermes V1 supports them natively. The project uses `expo@54.0.35` (React Native 0.81.5), whose bundled Hermes engine does not support native private fields and requires the Babel transform.

**Fix:** Pinned to `~54.0.11` to match the installed Expo SDK version. The v54 preset delegates to `@react-native/babel-preset` which correctly transforms private class fields for this version of Hermes.

---

### 5. Bumped `react-native-css-interop` from `0.2.5` → `0.2.6`

**Problem:** After the runtime crash was resolved, the app launched but showed a black screen with no errors. `nativewind@4.1.23` declares an exact peer dependency on `react-native-css-interop@0.2.6`. The NativeWind metro transformer (bundled inside nativewind) serializes compiled CSS into the bundle using the v0.2.6 wire format. With the runtime pinned to `0.2.5`, the format mismatch caused all NativeWind `className` styles to silently produce no output. Views rendered transparent over the dark NavigationContainer background.

**Fix:** Updated to `0.2.6` so the metro transformer and runtime are aligned.

---

### 6. Cleaned up stale `pnpm.overrides` block from `package.json`

**Problem:** A `pnpm.overrides` block containing `"@babel/generator": "7.26.9"` was present in `apps/mobile/package.json`. In pnpm, project-level overrides belong in `pnpm-workspace.yaml`, not `package.json`. The stale entry had no effect but was misleading.

**Fix:** Removed the block. The `@babel/generator` override is no longer needed now that the correct Babel versions are resolved.

---

### 7. App icon and adaptive icon upscaled to 1024×1024

**Problem:** `src/assets/images/icon.png` and `adaptive-icon.png` were 181×181 pixels. Apple App Store and EAS Build require the app icon to be exactly 1024×1024. The undersized asset caused the icon to appear missing in the installed app and would cause App Store submission rejection.

**Fix:** Upscaled both images to 1024×1024 using LANCZOS resampling. The geometric logo retained acceptable quality; a native 1024×1024 export from the original vector source is recommended for production submission.

> **Note:** `splash.png` remains at 407×116 and should be replaced with a proper 1284×2778 full-screen design before App Store submission.

---

## Files Changed

| File | Change |
|------|--------|
| `apps/mobile/package.json` | Removed Babel 8 JSX plugin from deps; added Babel 7 version to devDeps; downgraded `babel-preset-expo`; bumped `react-native-css-interop`; removed stale `pnpm.overrides` block |
| `apps/mobile/pnpm-workspace.yaml` | Added `@babel/types` override; removed stale `pnpm.overrides` block |
| `apps/mobile/pnpm-lock.yaml` | Updated to reflect all dependency changes |
| `apps/mobile/src/assets/images/icon.png` | Upscaled 181×181 → 1024×1024 |
| `apps/mobile/src/assets/images/adaptive-icon.png` | Upscaled 181×181 → 1024×1024 |
| `apps/mobile/eas.json` | Added (EAS build configuration) |
