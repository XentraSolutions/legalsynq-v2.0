jest.mock('@react-native-async-storage/async-storage', () => (
  require('@react-native-async-storage/async-storage/jest/async-storage-mock')
));

jest.mock('@sentry/react-native', () => {
  const React = require('react');

  return {
    addBreadcrumb: jest.fn(),
    captureException: jest.fn(),
    init: jest.fn(),
    mobileReplayIntegration: jest.fn(() => ({})),
    reactNavigationIntegration: jest.fn(() => ({
      registerNavigationContainer: jest.fn(),
    })),
    setContext: jest.fn(),
    setTag: jest.fn(),
    wrap: jest.fn((component) => component),
  };
});

jest.mock('@expo/vector-icons', () => {
  const React = require('react');
  const { Text } = require('react-native');

  return {
    FontAwesome6: ({ name }: { name: string }) => React.createElement(Text, null, name),
    Ionicons: ({ name }: { name: string }) => React.createElement(Text, null, name),
    MaterialCommunityIcons: ({ name, ...props }: { name: string; testID?: string }) =>
      React.createElement(Text, { ...props, name }, name),
  };
});
