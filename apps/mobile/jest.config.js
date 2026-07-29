module.exports = {
  preset: 'jest-expo',
  setupFilesAfterEnv: ['<rootDir>/jest.setup.tsx'],
  transformIgnorePatterns: [
    'node_modules/(?!\\.pnpm|((jest-)?react-native|@react-native(-community)?)|expo(nent)?|@expo(nent)?/.*|@expo-google-fonts/.*|react-navigation|@react-navigation/.*|@unimodules/.*|unimodules|sentry-expo|native-base|react-native-svg|nativewind|@gorhom/.*)',
  ],
  collectCoverageFrom: [
    'src/shared/components/Button/Button.tsx',
    'src/shared/services/Authentication/AuthenticationAdapter.ts',
    'src/shared/utils/date/dateUtils.ts',
    'src/shared/utils/formatting/*.ts',
    'src/shared/validation/authSchemas.ts',
    'src/features/liens/validation/lienValidation.ts',
    '!src/**/index.ts',
  ],
  coverageThreshold: {
    global: {
      statements: 80,
      functions: 80,
      branches: 80,
      lines: 80,
    },
  },
};
