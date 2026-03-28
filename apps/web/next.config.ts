import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  experimental: {
    serverActions: {
      allowedOrigins: ['localhost:3000'],
    },
  },
  async rewrites() {
    const gatewayUrl = process.env.GATEWAY_URL ?? 'http://localhost:5000';
    return [
      {
        source: '/api/:path*',
        destination: `${gatewayUrl}/:path*`,
      },
    ];
  },
};

export default nextConfig;
