import 'react-native-gesture-handler';
import './global.css';
import * as Sentry from '@sentry/react-native';

import App from './src/App/App';
import { sentryNavigationIntegration } from './src/shared/services/ErrorTracking/SentryNavigationIntegration';

Sentry.init({
  dsn: 'https://fc6f3bc520e786fa25be00c38ec0a2b5@o4511326066638848.ingest.us.sentry.io/4511631955853312',
  debug: __DEV__,

  // Adds more context data to events (IP address, cookies, user, etc.)
  // For more information, visit: https://docs.sentry.io/platforms/react-native/data-management/data-collected/
  sendDefaultPii: true,

  // Enable Logs
  enableLogs: true,

  // Configure Session Replay
  replaysSessionSampleRate: 0.1,
  replaysOnErrorSampleRate: 1,
  integrations: [
    Sentry.mobileReplayIntegration(),
    sentryNavigationIntegration,
  ],

  // uncomment the line below to enable Spotlight (https://spotlightjs.com)
  // spotlight: __DEV__,
});

export default Sentry.wrap(App);
