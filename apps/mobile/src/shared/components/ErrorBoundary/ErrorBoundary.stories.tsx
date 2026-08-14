import { Text } from 'react-native';

import { ErrorBoundary } from './ErrorBoundary';

export default { title: 'Shared/ErrorBoundary', component: ErrorBoundary };

export function Default() {
  return (
    <ErrorBoundary>
      <Text>Protected content</Text>
    </ErrorBoundary>
  );
}
