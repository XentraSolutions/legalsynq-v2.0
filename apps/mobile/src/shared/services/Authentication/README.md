# Authentication Service

The mobile authentication service calls the gateway-backed authentication API, stores the access
token and user session in Expo SecureStore, and mirrors the active session into `authAtom` for UI
navigation decisions.
