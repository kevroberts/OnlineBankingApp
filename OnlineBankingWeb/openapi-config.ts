import type { ConfigFile } from "@rtk-query/codegen-openapi";

// `npm run api:codegen` with the API running on its https profile. The output is committed.
const config: ConfigFile = {
  schemaFile: process.env.API_SCHEMA ?? "https://localhost:7294/openapi/v1.json",
  apiFile: "./src/api/baseApi.ts",
  apiImport: "baseApi",
  outputFile: "./src/api/bankApi.generated.ts",
  exportName: "generatedApi",
  hooks: false,
};

export default config;
