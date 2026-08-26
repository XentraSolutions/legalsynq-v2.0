const isProduction = process.env.EXPO_PUBLIC_APP_ENV === 'production';
const nativeDeepLinkConfig = [isProduction ? "applinks:links.legalsynq.net" : "applinks:links-qa.legalsynq.net"];

module.exports = {
  "expo": {
    "name": isProduction ? "LegalSynq" : "LegalSynq QA",
    "slug": "legalsynq",
    "version": "3.0.0",
    "orientation": "portrait",
    "icon": "./src/assets/images/icon.png",
    "userInterfaceStyle": "automatic",
    "ios": {
      "supportsTablet": false,
      "bundleIdentifier": isProduction ? "com.legalsynq" : "com.legalsynq.qa",
      "associatedDomains": nativeDeepLinkConfig,
      "infoPlist": {
        "ITSAppUsesNonExemptEncryption": false,
        "NSFaceIDUsageDescription": "Allow $(PRODUCT_NAME) to use Face ID to securely access your saved login."
      }
    },
    "android": {
      "adaptiveIcon": {
        "foregroundImage": "./src/assets/images/adaptive-icon.png",
        "backgroundColor": "#0d1f35"
      },
      "package": isProduction ? "com.legalsynq" : "com.legalsynq.qa",
      "intentFilters": nativeDeepLinkConfig,
      "permissions": [
        "android.permission.USE_BIOMETRIC",
        "android.permission.USE_FINGERPRINT"
      ]
    },
    "plugins": [
      [
        "expo-secure-store",
        {
          "configureAndroidBackup": false,
          "faceIDPermission": "Allow $(PRODUCT_NAME) to access your saved login using Face ID."
        }
      ],
      [
        "expo-local-authentication",
        {
          "faceIDPermission": "Allow $(PRODUCT_NAME) to use Face ID to sign in."
        }
      ],
      "expo-font",
      "@react-native-community/datetimepicker",
      [
        "expo-splash-screen",
        {
          "backgroundColor": "#0d1f35",
          "image": "./src/assets/images/splash.png",
          "imageWidth": 300,
          "resizeMode": "contain"
        }
      ],
      [
        "@sentry/react-native/expo",
        {
          "url": "https://sentry.io/",
          "project": "legal-synq-mobile-v3",
          "organization": "xentra-infotech-solutions-inc"
        }
      ]
    ],
    "extra": {
      "eas": {
        "projectId": "e30e217d-14d9-4aea-ae3a-de4b51be604e"
      }
    },
    "runtimeVersion": {
      "policy": "appVersion"
    },
    "updates": {
      "url": "https://u.expo.dev/e30e217d-14d9-4aea-ae3a-de4b51be604e"
    }
  }
};
