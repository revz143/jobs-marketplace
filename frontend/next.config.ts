import type { NextConfig } from "next";
import path from "node:path";
const nextConfig: NextConfig = {
  outputFileTracingRoot: path.join(__dirname),
  async rewrites() { return [{ source: "/backend/:path*", destination: "http://api:8080/:path*" }]; },
};
export default nextConfig;
