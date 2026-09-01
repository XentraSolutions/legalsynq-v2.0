# Authentication Service

The mobile authentication service calls the gateway-backed authentication API, stores the access
token and user session using the existing secure/local storage boundaries, and mirrors the active
session into `authAtom` for UI navigation decisions.

`authAtom.status` distinguishes startup `hydrating` from confirmed `authenticated` and
`unauthenticated` states. App bootstrap resolves the active API mode first, then hydrates a session
only when both its mode-appropriate token and user data exist. Current-mode tokens remain
memory-only, so a process restart resolves unauthenticated unless an existing complete stored
session is available. Clearing a session increments `sessionVersion`; auth-dependent in-memory
work uses that generation to prevent replay across logout, tenant changes, or mode switches.
