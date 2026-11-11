import type { NextConfig } from 'next'
const nextConfig: NextConfig = {
  reactCompiler: true,
  experimental: { serverActions: { bodySizeLimit: "6mb" } }, // 5 MB proof + multipart overhead.
  cacheComponents: true, // Stream dynamic Server Components behind Suspense instead of blocking navigation.
  partialPrefetching: true, // Prefetch only the public shell; member-specific money is fetched after navigation.
  agentRules: false, // Do not generate AGENTS.md while working in this coursework repository.
}
export default nextConfig
