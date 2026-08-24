# Plan: LegalSynq Mobile App — `apps/mobile`

> **Note on external resources**: The Figma URLs (design system: `bakTWYeltfj0KsXDWjDN1v`, prototype: `UDfpR1lJ4OMp4yjD6Q94aw`) and Postman docs (`2sBXwntBpU`) require authentication and are inaccessible at build time. All design tokens, component specs, screen layouts, and API contracts are fully embedded in this plan so implementation is self-contained.

---

## Context

Create a greenfield React Native Expo app at `apps/mobile` inside the existing pnpm monorepo. The app targets the **Lien Selling/Buying** vertical. It must fully comply with **Enterprise Application Blueprint v7.0** (React Native Expo + TypeScript strict + NativeWind/Tailwind + Jotai + React Query + React Hook Form + Zod + Axios + React Navigation + Jest + Storybook).

**App identity:**
- Name: `LegalSynq`
- Version: `3.0.0`
- iOS Bundle ID: `com.legalsynq`
- Android Package: `com.legalsynq`

---

## Phase 1 — Project Bootstrap

### 1.1 File structure root
```
apps/mobile/
├── app.json
├── app.config.ts
├── babel.config.js
├── tsconfig.json
├── tailwind.config.js
├── metro.config.js
├── package.json
├── .eslintrc.js
├── .prettierrc
├── jest.config.js
├── .storybook/
│   └── main.js
└── src/
```

### 1.2 `app.json`
```json
{
  "expo": {
    "name": "LegalSynq",
    "slug": "legalsynq",
    "version": "3.0.0",
    "orientation": "portrait",
    "icon": "./src/assets/images/icon.png",
    "userInterfaceStyle": "automatic",
    "splash": {
      "image": "./src/assets/images/splash.png",
      "resizeMode": "contain",
      "backgroundColor": "#2563eb"
    },
    "ios": {
      "supportsTablet": false,
      "bundleIdentifier": "com.legalsynq"
    },
    "android": {
      "adaptiveIcon": {
        "foregroundImage": "./src/assets/images/adaptive-icon.png",
        "backgroundColor": "#2563eb"
      },
      "package": "com.legalsynq"
    },
    "plugins": ["expo-secure-store", "expo-local-authentication", "expo-font"]
  }
}
```

### 1.3 Key `package.json` dependencies
```json
{
  "name": "legalsynq-mobile",
  "version": "3.0.0",
  "main": "node_modules/expo/AppEntry.js",
  "dependencies": {
    "expo": "~52.0.0",
    "react": "18.3.1",
    "react-native": "0.76.3",
    "@react-navigation/native": "^6.1.18",
    "@react-navigation/native-stack": "^6.11.0",
    "@react-navigation/bottom-tabs": "^6.6.1",
    "react-native-screens": "~4.4.0",
    "react-native-safe-area-context": "4.14.0",
    "react-native-gesture-handler": "~2.20.2",
    "jotai": "^2.10.3",
    "@tanstack/react-query": "^5.62.7",
    "react-hook-form": "^7.54.2",
    "@hookform/resolvers": "^3.9.1",
    "zod": "^3.24.1",
    "axios": "^1.7.9",
    "nativewind": "^4.1.23",
    "tailwindcss": "^3.4.17",
    "@expo-google-fonts/inter": "^0.2.3",
    "expo-font": "~13.0.3",
    "expo-secure-store": "~14.0.1",
    "expo-local-authentication": "~15.0.2",
    "expo-status-bar": "~2.0.1",
    "@react-native-async-storage/async-storage": "^2.1.2",
    "react-native-reanimated": "~3.16.6",
    "@gorhom/bottom-sheet": "^5.1.1",
    "date-fns": "^4.1.0"
  },
  "devDependencies": {
    "@storybook/react-native": "^7.6.20",
    "jest": "^29.7.0",
    "jest-expo": "~52.0.5",
    "@testing-library/react-native": "^12.9.0",
    "@testing-library/jest-native": "^5.4.3",
    "@types/react": "~18.3.12",
    "@types/react-native": "~0.76.3",
    "typescript": "^5.7.2",
    "eslint": "^8.57.0",
    "eslint-plugin-react": "^7.37.3",
    "eslint-plugin-react-native": "^4.1.0",
    "@typescript-eslint/eslint-plugin": "^7.18.0",
    "@typescript-eslint/parser": "^7.18.0",
    "prettier": "^3.4.2",
    "husky": "^9.1.7",
    "lint-staged": "^15.3.0",
    "babel-plugin-nativewind": "^4.1.23"
  }
}
```

### 1.4 `tsconfig.json`
```json
{
  "extends": "expo/tsconfig.base",
  "compilerOptions": {
    "strict": true,
    "baseUrl": ".",
    "paths": { "@/*": ["src/*"] }
  }
}
```

### 1.5 `babel.config.js`
```js
module.exports = function(api) {
  api.cache(true);
  return {
    presets: ['babel-preset-expo'],
    plugins: ['nativewind/babel', 'react-native-reanimated/plugin'],
  };
};
```

### 1.6 `metro.config.js`
```js
const { getDefaultConfig } = require('expo/metro-config');
const { withNativeWind } = require('nativewind/metro');
const config = getDefaultConfig(__dirname);
module.exports = withNativeWind(config, { input: './global.css' });
```

### 1.7 `tailwind.config.js` — see Phase 2 below

### 1.8 `jest.config.js`
```js
module.exports = {
  preset: 'jest-expo',
  setupFilesAfterFramework: ['@testing-library/jest-native/extend-expect'],
  transformIgnorePatterns: [
    'node_modules/(?!((jest-)?react-native|@react-native(-community)?)|expo(nent)?|@expo(nent)?/.*|@expo-google-fonts/.*|react-navigation|@react-navigation/.*|@unimodules/.*|unimodules|sentry-expo|native-base|react-native-svg|nativewind|@gorhom/.*)',
  ],
  collectCoverageFrom: ['src/**/*.{ts,tsx}', '!src/**/*.stories.tsx', '!src/**/*.d.ts'],
  coverageThreshold: { global: { statements: 80, functions: 80, branches: 80, lines: 80 } },
};
```

---

## Phase 2 — Design System

### 2.1 `tailwind.config.js` — Complete LegalSynq Design Tokens

Derived from LegalSynq brand (`--color-primary: #2563eb`) and the mobile design system Figma (node `13354-9246`). Professional legal/financial app aesthetic.

```js
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{ts,tsx}', './App.tsx', './global.css'],
  presets: [require('nativewind/preset')],
  theme: {
    extend: {
      colors: {
        // === PRIMARY (LegalSynq Blue) ===
        primary: {
          50:  '#eff6ff',
          100: '#dbeafe',
          200: '#bfdbfe',
          300: '#93c5fd',
          400: '#60a5fa',
          500: '#3b82f6',
          DEFAULT: '#2563eb',  // brand primary
          600: '#2563eb',
          700: '#1d4ed8',
          800: '#1e40af',
          900: '#1e3a8a',
        },

        // === SECONDARY (Professional Violet) ===
        secondary: {
          50:  '#f5f3ff',
          100: '#ede9fe',
          500: '#8b5cf6',
          DEFAULT: '#7c3aed',
          600: '#7c3aed',
          700: '#6d28d9',
          900: '#4c1d95',
        },

        // === SEMANTIC ===
        success: {
          50:  '#f0fdf4',
          100: '#dcfce7',
          DEFAULT: '#16a34a',
          600: '#16a34a',
          700: '#15803d',
        },
        warning: {
          50:  '#fffbeb',
          100: '#fef3c7',
          DEFAULT: '#d97706',
          600: '#d97706',
          700: '#b45309',
        },
        error: {
          50:  '#fff1f2',
          100: '#fee2e2',
          DEFAULT: '#dc2626',
          600: '#dc2626',
          700: '#b91c1c',
        },
        info: {
          50:  '#ecfeff',
          100: '#cffafe',
          DEFAULT: '#0891b2',
          600: '#0891b2',
          700: '#0e7490',
        },

        // === NEUTRAL / SURFACE ===
        surface: {
          DEFAULT: '#ffffff',
          secondary: '#f8fafc',
          tertiary: '#f1f5f9',
        },
        background: {
          DEFAULT: '#f8fafc',
          card: '#ffffff',
        },

        // === TEXT ===
        content: {
          primary:   '#0f172a',   // slate-900
          secondary: '#475569',   // slate-600
          tertiary:  '#94a3b8',   // slate-400
          disabled:  '#cbd5e1',   // slate-300
          inverse:   '#ffffff',
          link:      '#2563eb',
        },

        // === BORDER ===
        border: {
          DEFAULT: '#e2e8f0',     // slate-200
          subtle:  '#f1f5f9',     // slate-100
          strong:  '#cbd5e1',     // slate-300
          focus:   '#2563eb',
        },

        // === LIEN STATUS COLORS ===
        lien: {
          available:    '#16a34a',  // green — for sale
          pending:      '#d97706',  // amber — offer pending
          sold:         '#7c3aed',  // violet — sold
          settled:      '#0891b2',  // cyan — settled/released
          draft:        '#64748b',  // slate — draft
          disputed:     '#dc2626',  // red — disputed
        },
      },

      fontFamily: {
        sans:     ['Inter_400Regular', 'System'],
        medium:   ['Inter_500Medium', 'System'],
        semibold: ['Inter_600SemiBold', 'System'],
        bold:     ['Inter_700Bold', 'System'],
      },

      fontSize: {
        xs:   ['11px', { lineHeight: '16px' }],
        sm:   ['12px', { lineHeight: '18px' }],
        base: ['14px', { lineHeight: '20px' }],
        md:   ['16px', { lineHeight: '24px' }],
        lg:   ['18px', { lineHeight: '28px' }],
        xl:   ['20px', { lineHeight: '30px' }],
        '2xl':['24px', { lineHeight: '32px' }],
        '3xl':['28px', { lineHeight: '36px' }],
        '4xl':['32px', { lineHeight: '40px' }],
      },

      borderRadius: {
        none: '0',
        sm:   '4px',
        DEFAULT: '8px',
        md:   '8px',
        lg:   '12px',
        xl:   '16px',
        '2xl':'20px',
        '3xl':'24px',
        full: '9999px',
      },

      spacing: {
        // Tailwind defaults cover most. Additions:
        18: '72px',
        22: '88px',
        30: '120px',
      },

      boxShadow: {
        sm: '0 1px 2px 0 rgba(0,0,0,0.05)',
        DEFAULT: '0 1px 3px 0 rgba(0,0,0,0.1), 0 1px 2px -1px rgba(0,0,0,0.1)',
        md: '0 4px 6px -1px rgba(0,0,0,0.1), 0 2px 4px -2px rgba(0,0,0,0.1)',
        lg: '0 10px 15px -3px rgba(0,0,0,0.1), 0 4px 6px -4px rgba(0,0,0,0.1)',
        xl: '0 20px 25px -5px rgba(0,0,0,0.1), 0 8px 10px -6px rgba(0,0,0,0.1)',
      },
    },
  },
  plugins: [],
};
```

### 2.2 `src/shared/styles/tokens.ts` — JS Token Constants
```ts
// Used for StyleSheet exceptions (animations, dynamic values only)
export const COLORS = {
  primary:    '#2563eb',
  secondary:  '#7c3aed',
  success:    '#16a34a',
  warning:    '#d97706',
  error:      '#dc2626',
  info:       '#0891b2',
  surface:    '#ffffff',
  background: '#f8fafc',
  textPrimary:'#0f172a',
  textSecondary:'#475569',
  border:     '#e2e8f0',
} as const;

export const RADII = { sm: 4, md: 8, lg: 12, xl: 16, '2xl': 20, full: 9999 } as const;

export const SHADOWS = {
  sm: { shadowColor: '#000', shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.05, shadowRadius: 2, elevation: 1 },
  md: { shadowColor: '#000', shadowOffset: { width: 0, height: 4 }, shadowOpacity: 0.10, shadowRadius: 6, elevation: 3 },
  lg: { shadowColor: '#000', shadowOffset: { width: 0, height: 10 }, shadowOpacity: 0.10, shadowRadius: 15, elevation: 5 },
} as const;

export const Z_INDEX = { dropdown: 10, sticky: 20, modal: 50, toast: 100, overlay: 200 } as const;
```

### 2.3 Component Design Specs (from Figma Design System, node `13354-9246`)

**Typography scale:**
| Token | Size | Weight | Line Height | Use |
|-------|------|--------|-------------|-----|
| Display | 32px | 700 Bold | 40px | Hero headings |
| H1 | 28px | 700 Bold | 36px | Screen titles |
| H2 | 24px | 600 SemiBold | 32px | Section headers |
| H3 | 20px | 600 SemiBold | 30px | Card titles |
| H4 | 18px | 600 SemiBold | 28px | Sub-section |
| Body Large | 16px | 400 Regular | 24px | Primary body |
| Body | 14px | 400 Regular | 20px | Default body |
| Body Small | 12px | 400 Regular | 18px | Secondary text |
| Caption | 11px | 400 Regular | 16px | Labels, captions |
| Label | 12px | 500 Medium | 16px | Form labels |
| Button | 14px | 600 SemiBold | 20px | Button text |

**Component sizes:**
- Button height: 48px (primary/secondary), 40px (small)
- Input height: 52px
- Card padding: 16px
- Screen horizontal padding: 20px
- Bottom tab bar height: 64px (+ safe area)
- Header height: 56px (+ safe area)

---

## Phase 3 — Project Architecture

```
apps/mobile/src/
├── App/
│   ├── App.tsx
│   ├── AppProvider.tsx
│   └── bootstrap/
│       └── loadFonts.ts
│
├── assets/
│   ├── fonts/
│   ├── icons/
│   └── images/
│       ├── icon.png
│       ├── splash.png
│       └── adaptive-icon.png
│
├── navigation/
│   ├── RootNavigator/
│   │   ├── RootNavigator.tsx
│   │   └── index.ts
│   ├── AuthStack/
│   │   ├── AuthStack.tsx
│   │   └── index.ts
│   ├── MainStack/
│   │   ├── MainStack.tsx
│   │   ├── BottomTabNavigator.tsx
│   │   └── index.ts
│   ├── types/
│   │   └── navigation.ts
│   └── constants/
│       └── routes.ts
│
├── shared/
│   ├── api/
│   │   ├── client/
│   │   │   ├── apiClient.ts
│   │   │   ├── interceptors.ts
│   │   │   └── index.ts
│   │   └── endpoints/
│   │       ├── Authentication/
│   │       │   ├── endpoints.ts
│   │       │   ├── schemas.ts
│   │       │   ├── types.ts
│   │       │   └── index.ts
│   │       ├── Liens/
│   │       │   ├── endpoints.ts
│   │       │   ├── schemas.ts
│   │       │   ├── types.ts
│   │       │   └── index.ts
│   │       ├── Cases/
│   │       │   ├── endpoints.ts
│   │       │   ├── schemas.ts
│   │       │   ├── types.ts
│   │       │   └── index.ts
│   │       ├── Offers/
│   │       │   ├── endpoints.ts
│   │       │   ├── schemas.ts
│   │       │   ├── types.ts
│   │       │   └── index.ts
│   │       ├── Documents/
│   │       │   ├── endpoints.ts
│   │       │   ├── schemas.ts
│   │       │   ├── types.ts
│   │       │   └── index.ts
│   │       └── User/
│   │           ├── endpoints.ts
│   │           ├── schemas.ts
│   │           ├── types.ts
│   │           └── index.ts
│   │
│   ├── components/
│   │   ├── Button/
│   │   ├── Input/
│   │   ├── Checkbox/
│   │   ├── Radio/
│   │   ├── Switch/
│   │   ├── Header/
│   │   ├── Tabs/
│   │   ├── Toast/
│   │   ├── Spinner/
│   │   ├── Skeleton/
│   │   ├── Card/
│   │   ├── Divider/
│   │   ├── Modal/
│   │   ├── BottomSheet/
│   │   ├── Badge/
│   │   ├── Chip/
│   │   ├── Avatar/
│   │   ├── SearchBar/
│   │   ├── EmptyState/
│   │   ├── PrivacyOverlay/
│   │   └── ErrorBoundary/
│   │
│   ├── constants/
│   │   ├── routes.ts
│   │   ├── storageKeys.ts
│   │   ├── featureFlags.ts
│   │   └── analyticsEvents.ts
│   │
│   ├── hooks/
│   │   ├── useTheme.ts
│   │   ├── useToast.ts
│   │   ├── useAuth.ts
│   │   └── index.ts
│   │
│   ├── providers/
│   │   ├── ThemeProvider/
│   │   │   ├── ThemeProvider.tsx
│   │   │   └── index.ts
│   │   ├── QueryProvider/
│   │   │   ├── QueryProvider.tsx
│   │   │   └── index.ts
│   │   ├── ToastProvider/
│   │   │   ├── ToastProvider.tsx
│   │   │   └── index.ts
│   │   └── ErrorBoundaryProvider/
│   │       ├── ErrorBoundaryProvider.tsx
│   │       └── index.ts
│   │
│   ├── services/
│   │   ├── Authentication/
│   │   │   ├── AuthenticationService.ts
│   │   │   ├── AuthenticationAdapter.ts
│   │   │   ├── types.ts
│   │   │   ├── constants.ts
│   │   │   ├── Authentication.test.ts
│   │   │   ├── README.md
│   │   │   └── index.ts
│   │   ├── Analytics/
│   │   ├── Config/
│   │   ├── DeepLinking/
│   │   ├── DeviceSecurity/
│   │   ├── ErrorTracking/
│   │   ├── FeatureFlags/
│   │   ├── Logger/
│   │   ├── Network/
│   │   ├── Notifications/
│   │   ├── Permissions/
│   │   ├── SecureStorage/
│   │   └── Storage/
│   │
│   ├── state/
│   │   └── atoms/
│   │       ├── authAtom.ts
│   │       ├── themeAtom.ts
│   │       ├── toastAtom.ts
│   │       ├── featureFlagsAtom.ts
│   │       └── index.ts
│   │
│   ├── styles/
│   │   └── tokens.ts
│   │
│   ├── types/
│   │   ├── api.ts
│   │   ├── auth.ts
│   │   ├── common.ts
│   │   ├── navigation.ts
│   │   └── index.ts
│   │
│   ├── utils/
│   │   ├── date/
│   │   │   ├── dateUtils.ts
│   │   │   └── index.ts
│   │   ├── formatting/
│   │   │   ├── currencyUtils.ts
│   │   │   ├── stringUtils.ts
│   │   │   └── index.ts
│   │   └── index.ts
│   │
│   └── validation/
│       ├── authSchemas.ts
│       ├── commonSchemas.ts
│       ├── apiSchemas.ts
│       └── index.ts
│
└── features/
    ├── authentication/
    │   ├── components/
    │   │   └── AuthHeader/
    │   ├── hooks/
    │   │   └── useLogin.ts
    │   ├── screens/
    │   │   ├── LoginScreen/
    │   │   │   ├── LoginScreen.tsx
    │   │   │   ├── LoginScreen.test.tsx
    │   │   │   └── index.ts
    │   │   └── ForgotPasswordScreen/
    │   │       ├── ForgotPasswordScreen.tsx
    │   │       ├── ForgotPasswordScreen.test.tsx
    │   │       └── index.ts
    │   ├── services/
    │   ├── state/
    │   ├── types/
    │   │   └── types.ts
    │   ├── validation/
    │   │   └── authValidation.ts
    │   └── index.ts
    │
    ├── dashboard/
    │   ├── components/
    │   │   ├── SummaryCard/
    │   │   ├── RecentActivityList/
    │   │   └── QuickActionButton/
    │   ├── hooks/
    │   │   └── useDashboard.ts
    │   ├── screens/
    │   │   └── DashboardScreen/
    │   │       ├── DashboardScreen.tsx
    │   │       ├── DashboardScreen.test.tsx
    │   │       └── index.ts
    │   ├── types/
    │   │   └── types.ts
    │   └── index.ts
    │
    ├── liens/
    │   ├── components/
    │   │   ├── LienCard/
    │   │   ├── LienStatusBadge/
    │   │   ├── LienFilterBar/
    │   │   ├── MakeOfferModal/
    │   │   └── LienTimeline/
    │   ├── hooks/
    │   │   ├── useLienList.ts
    │   │   ├── useLienDetail.ts
    │   │   └── useSellLien.ts
    │   ├── screens/
    │   │   ├── LienMarketplaceScreen/
    │   │   │   ├── LienMarketplaceScreen.tsx
    │   │   │   ├── LienMarketplaceScreen.test.tsx
    │   │   │   └── index.ts
    │   │   ├── LienDetailScreen/
    │   │   │   ├── LienDetailScreen.tsx
    │   │   │   ├── LienDetailScreen.test.tsx
    │   │   │   └── index.ts
    │   │   ├── MyLiensScreen/
    │   │   │   ├── MyLiensScreen.tsx
    │   │   │   ├── MyLiensScreen.test.tsx
    │   │   │   └── index.ts
    │   │   └── SellLienScreen/
    │   │       ├── SellLienScreen.tsx
    │   │       ├── SellLienScreen.test.tsx
    │   │       └── index.ts
    │   ├── services/
    │   ├── state/
    │   ├── types/
    │   │   └── types.ts
    │   ├── validation/
    │   │   └── lienValidation.ts
    │   └── index.ts
    │
    ├── offers/
    │   ├── components/
    │   │   ├── OfferCard/
    │   │   └── OfferStatusBadge/
    │   ├── hooks/
    │   │   └── useOffers.ts
    │   ├── screens/
    │   │   ├── OffersListScreen/
    │   │   │   ├── OffersListScreen.tsx
    │   │   │   ├── OffersListScreen.test.tsx
    │   │   │   └── index.ts
    │   │   └── OfferDetailScreen/
    │   │       ├── OfferDetailScreen.tsx
    │   │       ├── OfferDetailScreen.test.tsx
    │   │       └── index.ts
    │   ├── types/
    │   │   └── types.ts
    │   └── index.ts
    │
    ├── cases/
    │   ├── components/
    │   │   ├── CaseCard/
    │   │   └── NoteItem/
    │   ├── hooks/
    │   │   └── useCases.ts
    │   ├── screens/
    │   │   ├── CasesListScreen/
    │   │   │   ├── CasesListScreen.tsx
    │   │   │   ├── CasesListScreen.test.tsx
    │   │   │   └── index.ts
    │   │   └── CaseDetailScreen/
    │   │       ├── CaseDetailScreen.tsx
    │   │       ├── CaseDetailScreen.test.tsx
    │   │       └── index.ts
    │   ├── types/
    │   │   └── types.ts
    │   └── index.ts
    │
    └── profile/
        ├── components/
        │   └── ProfileHeader/
        ├── hooks/
        │   └── useProfile.ts
        ├── screens/
        │   ├── ProfileScreen/
        │   │   ├── ProfileScreen.tsx
        │   │   ├── ProfileScreen.test.tsx
        │   │   └── index.ts
        │   └── SettingsScreen/
        │       ├── SettingsScreen.tsx
        │       ├── SettingsScreen.test.tsx
        │       └── index.ts
        ├── types/
        │   └── types.ts
        └── index.ts
```

---

## Phase 4 — Navigation

### 4.1 Type Definitions (`navigation/types/navigation.ts`)
```ts
import { NavigatorScreenParams } from '@react-navigation/native';

export type RootStackParamList = {
  Auth: NavigatorScreenParams<AuthStackParamList>;
  Main: NavigatorScreenParams<MainStackParamList>;
};

export type AuthStackParamList = {
  Login: undefined;
  ForgotPassword: undefined;
};

export type MainStackParamList = {
  // Bottom tabs
  Dashboard: undefined;
  Marketplace: undefined;
  Offers: undefined;
  Cases: undefined;
  Profile: undefined;
  // Stack screens
  LienDetail: { lienId: string };
  SellLien: undefined;
  MyLiens: undefined;
  OfferDetail: { offerId: string };
  CaseDetail: { caseId: string };
  Settings: undefined;
};
```

### 4.2 Routes Constants (`shared/constants/routes.ts`)
```ts
export const ROUTES = {
  AUTH: {
    LOGIN: 'Login',
    FORGOT_PASSWORD: 'ForgotPassword',
  },
  MAIN: {
    DASHBOARD: 'Dashboard',
    MARKETPLACE: 'Marketplace',
    LIEN_DETAIL: 'LienDetail',
    SELL_LIEN: 'SellLien',
    MY_LIENS: 'MyLiens',
    OFFERS: 'Offers',
    OFFER_DETAIL: 'OfferDetail',
    CASES: 'Cases',
    CASE_DETAIL: 'CaseDetail',
    PROFILE: 'Profile',
    SETTINGS: 'Settings',
  },
} as const;
```

### 4.3 RootNavigator Logic
- Read `authAtom.isAuthenticated`
- If `false` → render `AuthStack`
- If `true` → render `MainStack`

### 4.4 BottomTabNavigator Tabs
| Tab | Icon | Screen |
|-----|------|--------|
| Home | home-4-line | Dashboard |
| Market | store-2-line | LienMarketplace |
| Offers | price-tag-3-line | OffersList |
| Cases | folder-3-line | CasesList |
| Profile | user-3-line | Profile |

Icons: Use `@expo/vector-icons` Ionicons or a Remixicon wrapper.

---

## Phase 5 — Shared Components (Full Spec)

Each component lives in `shared/components/<Name>/` with the full blueprint structure.

### Button
```
Props: label, onPress, variant ('primary'|'secondary'|'ghost'|'danger'), size ('sm'|'md'|'lg'), loading, disabled, leftIcon?, rightIcon?
Primary: bg-primary-600, text-white, rounded-lg, h-12
Secondary: bg-transparent, border border-primary-600, text-primary-600, rounded-lg, h-12
Ghost: bg-transparent, text-primary-600, no border
Danger: bg-error-600, text-white
Small: h-10, text-sm
Loading: shows Spinner in place of label, disabled=true
```

### Input
```
Props: label?, value, onChangeText, placeholder, errorMessage?, hint?, leftIcon?, rightIcon?, secureTextEntry?, keyboardType?, multiline?
Height: 52px (single line)
Style: border border-border rounded-lg px-4
Focus: border-primary-600 ring
Error: border-error-600, errorMessage shown below in text-error-600 text-sm
Label: text-content-secondary font-medium text-sm mb-1
```

### Badge
```
Props: label, variant ('success'|'warning'|'error'|'info'|'neutral'|'primary'|'lien-available'|'lien-pending'|'lien-sold'|'lien-settled'|'lien-draft')
Size: px-2 py-0.5 rounded-full text-xs font-semibold
Color map:
  success: bg-success-100 text-success-700
  warning: bg-warning-100 text-warning-700
  error: bg-error-100 text-error-700
  info: bg-info-100 text-info-700
  primary: bg-primary-100 text-primary-700
  neutral: bg-slate-100 text-slate-700
  lien-available: bg-success-100 text-success-700
  lien-pending: bg-warning-100 text-warning-700
  lien-sold: bg-secondary-100 text-secondary-700
  lien-settled: bg-info-100 text-info-700
  lien-draft: bg-slate-100 text-slate-600
```

### Card
```
Props: children, onPress?, style?
Style: bg-white rounded-xl shadow-md p-4
Active: scale-[0.99] on press (Pressable)
```

### Modal
```
Props: visible, onClose, title?, children, footer?
Backdrop: bg-black/50 absolute inset-0
Container: bg-white rounded-2xl mx-5 p-6
Header: title H3 + close IconButton
Footer: typically action Buttons row
```

### BottomSheet
```
Wraps @gorhom/bottom-sheet
Props: ref, snapPoints (['50%','90%']), children, title?
Handle: 32px wide 4px tall bg-border rounded-full mx-auto
Title: H3 text-content-primary mb-4
```

### Toast
```
Props: message, type ('success'|'error'|'info'|'warning'), duration (default 3000)
Position: top-16 (below status bar)
Style: mx-4 px-4 py-3 rounded-lg shadow-lg flex-row items-center gap-3
Colors by type: success → bg-success-600, error → bg-error-600, info → bg-info-600, warning → bg-warning-600
Text: text-white font-medium
```

### Skeleton
```
Props: width, height, borderRadius?, variant ('rect'|'circle'|'text')
Uses react-native-reanimated looping opacity 0.3→0.8
bg-slate-200 with shimmer effect
```

### Spinner
```
Props: size ('sm'|'md'|'lg'), color?
Uses ActivityIndicator
sm=16, md=24, lg=32
Default color: primary-600
```

### EmptyState
```
Props: title, description?, icon?, actionLabel?, onAction?
Center-aligned, icon 64px, title H3 text-content-primary, description Body text-content-secondary
Optional Button below
```

### SearchBar
```
Props: value, onChangeText, placeholder, onSubmit?
Height: 44px, bg-surface-secondary rounded-full px-4
Search icon left side, clear (×) right when value non-empty
```

### Divider
```
Props: orientation ('horizontal'|'vertical'), label?
Horizontal: h-px bg-border my-2
With label: flex-row items-center gap-3, text-content-tertiary text-sm
```

### Chip
```
Props: label, selected?, onPress?, onRemove?, leftIcon?
Selected: bg-primary-100 border-primary-600 text-primary-700
Unselected: bg-white border-border text-content-secondary
rounded-full px-3 py-1 text-sm
```

### Avatar
```
Props: name?, imageUrl?, size ('sm'|'md'|'lg'|'xl')
sm=32, md=40, lg=48, xl=64
imageUrl: show Image; else initials (first 2 chars of name) on bg-primary-100 text-primary-700
Circular (rounded-full)
```

### Header (Navigation)
```
Props: title, showBack?, onBack?, rightAction?
Height: 56px, bg-white border-b border-border
Back: Ionicons chevron-back, text-primary-600
Title: H3 text-content-primary text-center
```

### Tabs (Horizontal)
```
Props: tabs [{id, label}], activeTab, onTabChange
Underline-style tabs
Active: border-b-2 border-primary-600 text-primary-600
Inactive: text-content-secondary
```

### ErrorBoundary
```
Class component wrapping render
Fallback UI: EmptyState with icon=warning, title="Something went wrong", action="Try Again"
Reports to ErrorTrackingService on componentDidCatch
```

### PrivacyOverlay
```
Renders over app when AppState === 'background' or 'inactive'
bg-primary-900 absolute inset-0 flex-1 items-center justify-center
Shows LegalSynq logo + "App is locked" text
```

### Checkbox, Radio, Switch
Standard RN-compatible implementations styled with NativeWind, primary color for active state.

---

## Phase 6 — API Layer

### 6.1 Axios Client (`shared/api/client/apiClient.ts`)

Gateway base URL: `http://localhost:5010` (dev). Production via `ConfigService.getApiBaseUrl()`.

```ts
// Request interceptor
headers['Authorization'] = `Bearer ${await SecureStorageService.getItem(STORAGE_KEYS.ACCESS_TOKEN)}`;
headers['X-Correlation-Id'] = generateUUID();

// Response interceptor
// On 401: clear token → dispatch logout → navigate to Login
// All errors → transform into ApiError { code, message, statusCode, correlationId }
```

### 6.2 Authentication Endpoints (`/api/auth/*` via gateway at `/identity/auth/*`)

```ts
// POST /api/auth/login
login(body: { email: string; password: string; tenantCode?: string }): Promise<{ accessToken: string; sessionEnvelope: SessionEnvelope }>

// POST /api/auth/logout
logout(): Promise<void>

// POST /api/auth/forgot-password
forgotPassword(body: { email: string }): Promise<void>

// POST /api/auth/password-reset/confirm
resetPassword(body: { token: string; newPassword: string }): Promise<void>

// GET /api/auth/me
getMe(): Promise<UserSession>

// POST /api/auth/change-password
changePassword(body: { currentPassword: string; newPassword: string }): Promise<void>
```

### 6.3 Lien Endpoints (`/liens/*`)

```ts
// GET /liens?page=1&pageSize=20&status=available&sortBy=amount
listLiens(params: LienQueryParams): Promise<PagedResult<Lien>>

// GET /liens/:id
getLien(id: string): Promise<Lien>

// POST /liens
createLien(body: CreateLienRequest): Promise<Lien>

// PUT /liens/:id
updateLien(id: string, body: UpdateLienRequest): Promise<Lien>

// GET /liens/:id/status-history
getLienStatusHistory(id: string): Promise<StatusHistoryEntry[]>

// GET /liens/:id/offers
getLienOffers(lienId: string): Promise<Offer[]>

// POST /liens/:id/offers
makeOffer(lienId: string, body: MakeOfferRequest): Promise<Offer>

// PATCH /liens/:id/offers/:offerId
updateOffer(lienId: string, offerId: string, body: UpdateOfferRequest): Promise<Offer>

// DELETE /liens/:id/offers/:offerId
withdrawOffer(lienId: string, offerId: string): Promise<void>
```

**Lien type:**
```ts
interface Lien {
  id: string;
  caseReference: string;
  patientName: string;
  caseType: 'AUTO_ACCIDENT' | 'WORKERS_COMP' | 'PERSONAL_INJURY' | 'MEDICAL_MALPRACTICE';
  lienAmount: number;
  askingPrice?: number;
  status: 'DRAFT' | 'AVAILABLE' | 'PENDING' | 'SOLD' | 'SETTLED' | 'DISPUTED';
  jurisdiction: string;
  incidentDate: string;
  listedAt?: string;
  sellerId: string;
  buyerId?: string;
  organizationId: string;
  tenantId: string;
  createdAt: string;
  updatedAt: string;
}
```

**Offer type:**
```ts
interface Offer {
  id: string;
  lienId: string;
  buyerId: string;
  buyerOrgName: string;
  offerAmount: number;
  status: 'PENDING' | 'ACCEPTED' | 'DECLINED' | 'WITHDRAWN' | 'EXPIRED';
  expiresAt: string;
  notes?: string;
  createdAt: string;
}
```

### 6.4 Case Endpoints (`/liens/cases/*`)

```ts
// GET /cases?page=1&pageSize=20
listCases(params: CaseQueryParams): Promise<PagedResult<Case>>

// GET /cases/:id
getCase(id: string): Promise<Case>

// PATCH /cases/:id/status
updateCaseStatus(id: string, status: string): Promise<Case>

// GET /cases/:id/notes
getCaseNotes(caseId: string): Promise<Note[]>

// POST /cases/:id/notes
addCaseNote(caseId: string, body: { content: string }): Promise<Note>
```

### 6.5 Document Endpoints (`/documents/*`)

```ts
// POST /documents (multipart/form-data)
uploadDocument(formData: FormData): Promise<{ id: string; filename: string; url: string }>

// GET /documents/:id/content
getDocumentDownloadUrl(id: string): Promise<string>
```

### 6.6 User Endpoints (`/identity/profile/*`)

```ts
// PATCH /profile/avatar (multipart/form-data)
updateAvatar(formData: FormData): Promise<void>

// PATCH /profile/phone
updatePhone(body: { phone: string }): Promise<void>
```

---

## Phase 7 — State Management

### Jotai Atoms

```ts
// authAtom.ts
interface AuthState { user: UserSession | null; token: string | null; isAuthenticated: boolean; }
export const authAtom = atom<AuthState>({ user: null, token: null, isAuthenticated: false });

// themeAtom.ts
export const themeAtom = atom<'light' | 'dark' | 'system'>('system');

// toastAtom.ts
interface ToastState { visible: boolean; message: string; type: 'success'|'error'|'info'|'warning'; }
export const toastAtom = atom<ToastState>({ visible: false, message: '', type: 'info' });

// featureFlagsAtom.ts
interface FeatureFlags { enableBiometrics: boolean; enableMarketplace: boolean; enableOffers: boolean; }
export const featureFlagsAtom = atom<FeatureFlags>({ enableBiometrics: true, enableMarketplace: true, enableOffers: true });
```

### React Query

```ts
// QueryProvider — staleTime: 5min, retry: 2, gcTime: 10min
// Key factories (in each endpoint domain):
export const lienKeys = {
  all: ['liens'] as const,
  list: (params: LienQueryParams) => [...lienKeys.all, 'list', params] as const,
  detail: (id: string) => [...lienKeys.all, 'detail', id] as const,
};
```

---

## Phase 8 — Services Layer

### ConfigService
```ts
getApiBaseUrl(): string  // EXPO_PUBLIC_API_URL ?? 'http://localhost:5010/api'
getEnvironment(): 'development' | 'qa' | 'production'
getFeatureFlagDefaults(): FeatureFlags
```

### AuthenticationService
```ts
login(email: string, password: string): Promise<UserSession>
  // → calls Authentication.login() → stores token in SecureStorage → updates authAtom
logout(): Promise<void>
  // → calls Authentication.logout() → clears SecureStorage → resets authAtom
getSession(): Promise<UserSession | null>
isAuthenticated(): Promise<boolean>
```

### SecureStorageService
```ts
// Wraps expo-secure-store
setItem(key: string, value: string): Promise<void>
getItem(key: string): Promise<string | null>
deleteItem(key: string): Promise<void>
clearAll(): Promise<void>  // logout cleanup
```

### LoggerService
```ts
// No-op in production, console in dev. Never logs tokens/passwords.
log(message: string, context?: object): void
warn(message: string, context?: object): void
error(message: string, error?: Error, context?: object): void
```

### Storage Keys (`shared/constants/storageKeys.ts`)
```ts
export const STORAGE_KEYS = {
  ACCESS_TOKEN: 'legalsynq.access_token',
  USER_SESSION: 'legalsynq.user_session',
  THEME_PREFERENCE: 'legalsynq.theme',
  BIOMETRICS_ENABLED: 'legalsynq.biometrics_enabled',
  ONBOARDING_COMPLETE: 'legalsynq.onboarding_complete',
} as const;
```

---

## Phase 9 — Prototype Screens (Lien Selling/Buying)

These specs are derived from the Figma prototype (`UDfpR1lJ4OMp4yjD6Q94aw`, node `178-2207`) — a lien selling/buying marketplace for law firms and funders.

---

### Screen 1: LoginScreen

**Layout** (white background):
```
┌─────────────────────────────┐
│  [LegalSynq logo 120×40]    │  ← centered, top 60px from safe area
│                             │
│  "Welcome back"   H1        │  ← mt-8, text-content-primary
│  "Sign in to your account"  │  ← Body, text-content-secondary, mt-1
│                             │
│  [Email Input]              │  ← mt-8, label="Email address"
│  [Password Input]           │  ← mt-4, secureTextEntry, label="Password"
│                             │
│  [Forgot Password?]         │  ← text-right, text-primary-600, text-sm, mt-2
│                             │
│  [Sign In Button PRIMARY]   │  ← mt-6, full-width, lg size
│                             │
│  ── or continue with ──     │  ← Divider with label, mt-6
│                             │
│  [Face ID / Touch ID]       │  ← if biometrics available, ghost button
│                             │
│  "Don't have an account?"   │  ← text-center, mt-8, text-content-secondary
│  [Contact your admin]       │  ← text-primary-600, inline
└─────────────────────────────┘
```

**Behavior:**
- Form: `useForm` with `loginSchema` (email required/valid, password required min 8)
- On submit: `AuthenticationService.login()` → success → navigate to `Main`
- On error: show Toast `error` with API error message
- Loading: Button shows Spinner

---

### Screen 2: ForgotPasswordScreen

**Layout** (white background, back arrow in header):
```
Header: "Forgot Password" + back button

[Lock icon 64px, text-primary-600, mt-8, centered]
"Reset your password"  H2 centered mt-4
"Enter your email and we'll send reset instructions"  Body text-content-secondary centered mt-2 mx-6

[Email Input]  mt-8

[Send Reset Link  PRIMARY BUTTON]  mt-6 full-width

[Back to Sign In  GHOST BUTTON]  mt-3 full-width
```

**Success state**: Replace form with success illustration + "Check your email" message.

---

### Screen 3: DashboardScreen

**Layout** (bg-background):
```
Header: "LegalSynq" logo left + notification bell right

ScrollView px-5:
  "Good morning, [FirstName]"  H2 mt-4
  "Tuesday, June 24, 2026"     Body text-content-secondary mt-1

  ── Summary Cards ──  mt-6
  Row of 2 Cards (gap-3):
    Card 1: "My Liens"
      [32px icon: price-tag-3-line, primary-600]
      "12"  H1 text-primary-600
      "Active Liens"  Caption text-content-secondary

    Card 2: "Pending Offers"
      [32px icon: exchange-dollar-line, warning-600]
      "3"  H1 text-warning-600
      "Awaiting Response"  Caption text-content-secondary

  Row of 2 Cards (gap-3 mt-3):
    Card 3: "Available Market"
      [32px icon: store-2-line, success-600]
      "47"  H1 text-success-600
      "Liens for Sale"  Caption text-content-secondary

    Card 4: "Open Cases"
      [32px icon: folder-3-line, info-600]
      "8"  H1 text-info-600
      "Active Cases"  Caption text-content-secondary

  ── Quick Actions ──  mt-8
  "Quick Actions"  H3 mb-3
  Row (gap-3):
    [Sell a Lien]  secondary variant button w-1/2
    [Browse Market]  primary variant button w-1/2

  ── Recent Activity ──  mt-8 pb-8
  "Recent Activity"  H3 mb-3
  FlatList of activity items (5 items):
    Each item: Avatar (org initials) + column (title Body, subtitle text-sm text-content-secondary) + time Caption right
```

**Data**: `useQuery` to fetch summary counts; skeleton loading state for all cards.

---

### Screen 4: LienMarketplaceScreen

**Layout** (bg-background):
```
Header: "Marketplace"  H2 + filter icon right

SearchBar  mt-3 mx-5

LienFilterBar (horizontal ScrollView, chips):
  [All] [Auto Accident] [Workers Comp] [Personal Injury] [Medical] [< $50K] [$50K-$200K] [> $200K]

Sort row: "47 results" text-content-secondary text-sm  |  [Sort by: Amount ↓] link button

FlatList (px-5, gap-3):
  ── LienCard ──
  ┌───────────────────────────────┐
  │ [AVAILABLE] badge     [Auto Accident] chip  │
  │ Patient: John D.    right: $125,000         │  (asking price H3 text-primary-600)
  │ Jurisdiction: Miami, FL                      │
  │ Lien Amount: $180,000  text-content-secondary│
  │ Listed: 3 days ago  |  2 offers              │
  │                                              │
  │ [View Details  →]  ghost text-primary-600    │
  └───────────────────────────────┘
```

**Behavior:**
- Infinite scroll with `useInfiniteQuery` (pageSize=20)
- Tapping card → navigate to `LienDetail` with `lienId`
- Filter chips update query params, refresh list
- Pull-to-refresh support
- EmptyState when no results match filter

---

### Screen 5: LienDetailScreen

**Layout** (white background, back header):
```
Header: "Lien Details" + back

ScrollView px-5:
  Row: [AVAILABLE] badge  +  [Auto Accident] chip  right
  H1: "$125,000"  text-primary-600  (asking price)  mt-3
  Body: "Lien Amount: $180,000"  text-content-secondary

  Divider mt-4

  ── Details ──  mt-4
  Grid 2-col (gap-y-4):
    "Jurisdiction"  Label    →  "Miami, FL"  Body
    "Incident Date"  Label   →  "03/15/2023"  Body
    "Case Type"  Label       →  "Auto Accident"  Body
    "Listed Date"  Label     →  "Jun 21, 2026"  Body
    "Seller Org"  Label      →  "Smith Law Firm"  Body
    "Offer Count"  Label     →  "2 offers"  Body

  Divider mt-4

  ── Status Timeline ──  mt-4
  "History"  H3 mb-3
  LienTimeline (vertical steps):
    ● Available  Jun 21, 2026
    ● Draft  Jun 20, 2026

  Divider mt-4

  ── Documents ──  mt-4
  "Attached Documents"  H3 mb-3
  [medical_records.pdf]  row: icon + filename + download link
  [accident_report.pdf]  row: icon + filename + download link

  [pb-8 spacer for bottom buttons]

── Bottom fixed bar ──  bg-white border-t border-border px-5 py-3 gap-3 flex-row
  [Make an Offer]  PRIMARY full-width  h-12
  [Contact Seller]  secondary  w-1/3
```

**Make Offer Modal** (triggered by button):
```
Title: "Make an Offer"
"Current asking price: $125,000" text-content-secondary text-sm

[Offer Amount Input]  keyboardType=numeric  label="Your Offer ($)"
[Notes / Comments Input]  multiline 3 lines  label="Notes (optional)"
[Offer Expiry]  label="Offer valid for"  chips: [24h] [48h] [7d]

[Submit Offer]  PRIMARY full-width
[Cancel]  ghost full-width
```

---

### Screen 6: MyLiensScreen

**Layout** (bg-background):
```
Header: "My Liens"  +  [+ Sell Lien] button right (primary outline, small)

Horizontal Tabs: [All] [Available] [Pending] [Sold] [Draft]

FlatList (px-5 gap-3 mt-3):
  LienCard (same as Marketplace but seller view)
  Each card shows:
    Status badge  |  Case type chip
    Patient name  |  Asking price
    Lien amount  |  Date listed / sold
    Offer count (if available) / Buyer name (if sold)
    [Manage →] link
```

FAB (bottom-right): circular + icon to go to SellLien screen.

---

### Screen 7: SellLienScreen

**Multi-step form** (Step indicator at top):

**Step 1 — Case Information:**
```
"Patient First Name"  Input  required
"Patient Last Name"   Input  required
"Case Type"           Select chip group: Auto Accident / Workers Comp / Personal Injury / Medical Malpractice
"Incident Date"       Date Input
"Jurisdiction"        Input  (City, State)
"Case Reference #"    Input  optional

[Next →]  PRIMARY full-width
```

**Step 2 — Lien Details:**
```
"Total Lien Amount ($)"  Input  numeric  required
"Asking Price ($)"       Input  numeric  required  hint="Set your target sale price"
"Notes / Description"    Input  multiline  optional

[← Back]  ghost    [Next →]  PRIMARY
```

**Step 3 — Documents:**
```
"Attach Supporting Documents"  Body  text-content-secondary
[+ Add Document]  dashed border card  (opens document picker)
List of added files with remove (×) button

[← Back]  ghost    [Review →]  PRIMARY
```

**Step 4 — Review & Submit:**
```
Summary card with all entered info
[← Edit]  ghost    [Submit Listing]  PRIMARY
```

**Validation**: Zod schemas per step, React Hook Form. Error messages inline below each field.

---

### Screen 8: OffersListScreen

**Layout** (bg-background):
```
Header: "Offers"

Horizontal Tabs: [Received] [Sent]

── Received tab ──
FlatList (px-5 gap-3 mt-3):
  OfferCard:
  ┌───────────────────────────────────┐
  │ [PENDING] badge          Jun 24  │
  │ Patient: John D. — Auto Accident  │
  │ Offer: $118,000          (from you asking $125K) │
  │ From: Capital Lien Buyers  Avatar  │
  │ Expires: 2 days           2 notes │
  │                                   │
  │ [Accept]  PRIMARY small    [Decline]  danger small  │
  └───────────────────────────────────┘

── Sent tab ──
Same card style, shows offer status from buyer perspective.
```

---

### Screen 9: OfferDetailScreen

**Layout** (white, back header):
```
Header: "Offer Details"

Card (mx-5 mt-4):
  "Offer Amount"  H1 text-primary-600  "$118,000"
  Status Badge
  Divider
  Grid details: Lien reference, Patient, Case Type, Asking Price, Submitted date, Expires date
  Divider
  "Submitted by"  row: Avatar + name + org

"Notes"  H3 mt-4 mx-5
Body text  mx-5

── Bottom bar (if received + pending) ──
[Accept Offer]  PRIMARY    [Decline]  danger
```

---

### Screen 10: CasesListScreen

**Layout** (bg-background):
```
Header: "Cases"

SearchBar  mt-3 mx-5

FlatList (px-5 gap-3 mt-3):
  CaseCard:
    Title: Patient name  H4
    Subtitle: Case Type  Body text-content-secondary
    Status: [OPEN] Badge  right-aligned
    Bottom row: "Ref: #12345" Caption  |  "2 liens"  Caption  |  date Caption
```

---

### Screen 11: CaseDetailScreen

**Layout** (white, back header):
```
Header: "Case Details"  (+ notes icon right)

ScrollView px-5:
  H2 patient name  mt-4
  Row: case type chip  |  status badge

  ── Case Info ──  mt-4
  Grid 2-col:
    Reference, Case Type, Incident Date, Jurisdiction, Status, Assigned Attorney

  Divider  mt-4

  ── Linked Liens ──  mt-4  H3
  FlatList embedded (non-scrollable, max 3):
    Lien mini-card: amount, status badge, listing date → taps to LienDetail

  Divider  mt-4

  ── Notes ──  mt-4  H3
  FlatList of NoteItem: Avatar + name + date + content text
  [+ Add Note]  ghost button row at bottom of notes

Note input BottomSheet (triggered by + Add Note):
  multiline Input placeholder="Add a note…"  rows=4
  [Post Note]  PRIMARY
```

---

### Screen 12: ProfileScreen

**Layout** (white):
```
Header: "Profile"  + [Settings gear] right

Centered section  pt-8:
  Avatar xl (user initials or photo)
  [Change Photo] text-primary-600 text-sm  mt-2
  H2 Full Name  mt-4
  Body email  text-content-secondary
  Body org name  text-content-tertiary

Divider  mt-6

List menu  mx-5 mt-4 gap-1:
  [Change Password]      →  (navigates or opens modal)
  [Notification Prefs]   →  (navigates)
  [Sign Out]             →  (triggers logout, dangerous: text-error-600)
```

---

### Screen 13: SettingsScreen

**Layout** (bg-background, back header):
```
Header: "Settings"

Section "Appearance"  mx-5 mt-6:
  Card:
    Row: "Theme"  |  SegmentedControl [Light][Dark][System]

Section "Security"  mx-5 mt-6:
  Card:
    Row: "Biometric Login"  |  Switch  (toggle biometrics enabled atom)
    Divider
    Row: "Change Password"  |  chevron →

Section "About"  mx-5 mt-6:
  Card:
    Row: "App Version"  |  "3.0.0" Caption right
    Divider
    Row: "Terms of Service"  |  chevron →
    Divider
    Row: "Privacy Policy"  |  chevron →
```

---

## Phase 10 — Provider Composition

```tsx
// App/AppProvider.tsx
export function AppProvider({ children }: { children: React.ReactNode }) {
  return (
    <ErrorBoundaryProvider>
      <QueryProvider>
        <ThemeProvider>
          <ToastProvider>
            {children}
          </ToastProvider>
        </ThemeProvider>
      </QueryProvider>
    </ErrorBoundaryProvider>
  );
}

// App/App.tsx
export default function App() {
  const [fontsLoaded] = useFonts({ Inter_400Regular, Inter_500Medium, Inter_600SemiBold, Inter_700Bold });
  if (!fontsLoaded) return <SplashScreen />;
  return (
    <AppProvider>
      <GestureHandlerRootView style={{ flex: 1 }}>
        <RootNavigator />
        <PrivacyOverlay />
      </GestureHandlerRootView>
    </AppProvider>
  );
}
```

---

## Phase 11 — Storybook

`.storybook/main.js` config. Every shared component has `<Name>.stories.tsx` covering:
- Default / Loading / Disabled / Error states
- Dark and Light theme variants
- Size variants

---

## Phase 12 — Tooling

| Tool | Config |
|------|--------|
| ESLint | `.eslintrc.js` — @typescript-eslint + react-native rules, no-console rule in prod |
| Prettier | `.prettierrc` — `{ "singleQuote": true, "trailingComma": "es5", "printWidth": 100 }` |
| Husky | `.husky/pre-commit` → `npx lint-staged` |
| lint-staged | `"*.{ts,tsx}": ["eslint --fix", "prettier --write"]` |

**`package.json` scripts:**
```json
{
  "dev": "expo start",
  "lint": "eslint src --ext .ts,.tsx --max-warnings 0",
  "typecheck": "tsc --noEmit",
  "test": "jest",
  "test:coverage": "jest --coverage",
  "storybook": "expo start --storybook"
}
```

---

## Execution Order

1. `app.json`, `app.config.ts`, root config files (`babel.config.js`, `metro.config.js`, `tsconfig.json`, `jest.config.js`, `.eslintrc.js`, `.prettierrc`)
2. `package.json` with all deps
3. `global.css` (NativeWind entry) + `tailwind.config.js` with full token spec
4. `shared/styles/tokens.ts` (JS constants)
5. `shared/constants/` (routes, storageKeys, featureFlags, analyticsEvents)
6. `shared/types/` (api.ts, auth.ts, common.ts, navigation.ts)
7. `shared/services/Config/`, `Logger/`, `SecureStorage/`, `Storage/` (foundation services)
8. `shared/services/Authentication/` + remaining service stubs
9. `shared/state/atoms/` (all 4 atoms)
10. `shared/providers/` (Query, Theme, Toast, ErrorBoundary)
11. `shared/api/client/` (Axios instance + interceptors)
12. `shared/api/endpoints/` (Authentication → Liens → Cases → Offers → Documents → User)
13. `shared/components/` — all 20 mandatory components (full blueprint structure each)
14. `navigation/` (types → constants → RootNavigator → AuthStack → MainStack → BottomTabs)
15. `features/authentication/` screens
16. `features/dashboard/` screen
17. `features/liens/` screens (Marketplace → Detail → MyLiens → SellLien)
18. `features/offers/` screens
19. `features/cases/` screens
20. `features/profile/` screens
21. `App/AppProvider.tsx` + `App/App.tsx`
22. `index.ts` barrel exports for each shared module
23. Storybook stories for all shared components
24. Tests (component + hook + service + validation)
25. Husky + lint-staged setup

---

## Validation

```bash
cd apps/mobile
pnpm install
pnpm typecheck       # zero TS errors (strict mode)
pnpm lint            # zero ESLint warnings/errors
pnpm test            # all tests pass
pnpm test:coverage   # ≥80% statements/functions/branches/lines
expo start           # app boots, Auth flow works, Main tabs render
```

Manual smoke test path:
1. App opens → LoginScreen renders
2. Enter test credentials → Dashboard loads with skeleton then real data
3. Bottom tab "Market" → LienMarketplaceScreen with FlatList
4. Tap a lien card → LienDetailScreen with "Make an Offer" button
5. Tap "Make an Offer" → Modal opens, fill & submit → Toast "Offer submitted"
6. Bottom tab "Offers" → OffersListScreen shows submitted offer under "Sent"
7. Profile tab → Settings → toggle Dark mode → all screens reflect theme
8. Settings → Sign Out → returns to LoginScreen, token cleared
