module.exports = {
  "expo": {
    "name": process.env.EXPO_PUBLIC_APP_ENV === 'production' ? "LegalSynq" : "LegalSynq QA",
    "slug": "legalsynq",
    "version": "3.0.0",
    "orientation": "portrait",
    "icon": "./src/assets/images/icon.png",
    "userInterfaceStyle": "automatic",
    "ios": {
      "supportsTablet": false,
      "bundleIdentifier": process.env.EXPO_PUBLIC_APP_ENV === 'production' ? "com.legalsynq" : "com.legalsynq.qa",
      "infoPlist": {
        "ITSAppUsesNonExemptEncryption": false
      }
    },
    "android": {
      "adaptiveIcon": {
        "foregroundImage": "./src/assets/images/adaptive-icon.png",
        "backgroundColor": "#0d1f35"
      },
      "package": process.env.EXPO_PUBLIC_APP_ENV === 'production' ? "com.legalsynq" : "com.legalsynq.qa",
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
