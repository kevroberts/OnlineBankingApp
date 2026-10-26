import type { NextConfig } from "next";

// Only the Next.js server knows where the API is; the browser sees one origin.
const apiOrigin = process.env.API_ORIGIN ?? "https://localhost:7294";

const config: NextConfig = {
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiOrigin}/api/:path*` }];
  },
};

export default config;
