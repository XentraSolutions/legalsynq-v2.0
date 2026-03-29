/** @type {import('next').NextConfig} */
const nextConfig = {
  experimental: {
    serverActions: {
      allowedOrigins: ['*'],
    },
  },
  async rewrites() {
    const gatewayUrl = process.env.GATEWAY_URL ?? 'http://localhost:5010';
    return [
      {
        source: '/api/:path*',
        destination: `${gatewayUrl}/:path*`,
      },
    ];
  },
};

export default nextConfig;
