/** @type {import('next').NextConfig} */
const nextConfig = {
  allowedDevOrigins: ['*.spock.replit.dev', '*.replit.dev'],
  experimental: {
    serverActions: {
      // Next.js 14 CSRF check: the Replit dev proxy can cause origin/host
      // mismatches. allowedOrigins is set to allow all for development.
      // TODO: lock down to explicit origins for production.
      allowedOrigins: ['*'],
    },
  },
  async rewrites() {
    const gatewayUrl = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';
    return {
      // beforeFiles: run before pages/static — intentionally empty
      beforeFiles: [],
      // afterFiles: run after static files but before dynamic routes — empty so
      // that BFF catch-all route handlers (/api/careconnect/[...path] etc.)
      // are never bypassed
      afterFiles: [],
      // fallback: run only when NO static or dynamic route matches.
      // This lets direct /api/... calls reach the gateway for paths that do
      // NOT have a dedicated BFF handler.
      fallback: [
        {
          source: '/api/:path*',
          destination: `${gatewayUrl}/:path*`,
        },
      ],
    };
  },
};

export default nextConfig;
