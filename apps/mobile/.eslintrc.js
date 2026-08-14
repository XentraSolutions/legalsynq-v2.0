module.exports = {
  root: true,
  extends: ['eslint:recommended', 'plugin:react/recommended', 'plugin:react-native/all'],
  parser: '@typescript-eslint/parser',
  plugins: ['@typescript-eslint', 'react', 'react-native'],
  parserOptions: {
    ecmaVersion: 2022,
    sourceType: 'module',
    ecmaFeatures: {
      jsx: true,
    },
  },
  settings: {
    react: {
      version: 'detect',
    },
  },
  env: {
    es2022: true,
    jest: true,
  },
  rules: {
    'react/react-in-jsx-scope': 'off',
    'react/prop-types': 'off',
    'react-native/no-inline-styles': 'off',
    'no-undef': 'off',
    'no-unused-vars': 'off',
    '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
    'no-console': process.env.NODE_ENV === 'production' ? 'error' : 'off',
  },
};
