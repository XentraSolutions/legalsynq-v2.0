/** @type {import('next').NextConfig} */
const nextConfig = {
  experimental: {
    serverActions: {
      allowedOrigins: ['*'],
    },
  },
  async rewrites() {
    const gatewayUrl = process.env.GATEWAY_URL ?? 'http://localhost:5010';
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
