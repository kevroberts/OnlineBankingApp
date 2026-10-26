import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import { defineConfig, globalIgnores } from "eslint/config";

export default defineConfig([
  ...nextVitals,
  ...nextTs,
  globalIgnores([
    ".next/**",
    "out/**",
    "next-env.d.ts",
    "public/mockServiceWorker.js",
    "playwright-report/**",
    "test-results/**",
  ]),
  {
    files: ["src/api/bankApi.generated.ts"],
    rules: { "@typescript-eslint/no-explicit-any": "off" },
  },
]);
