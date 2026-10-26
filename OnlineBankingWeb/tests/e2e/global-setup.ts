import { request, type FullConfig } from "@playwright/test";

/** In the API's Demo environment, start every run from the seeded balances. Elsewhere the tests use relative checks. */
export default async function globalSetup(config: FullConfig) {
  const baseURL = config.projects[0]?.use.baseURL ?? "http://localhost:3000";
  const api = await request.newContext({ baseURL });
  try {
    const login = await api.post("/api/v1/auth/login", {
      data: { username: "alice", password: "Demo#2026" },
    });
    if (!login.ok()) return;
    const reset = await api.post("/api/v1/demo/reset");
    console.log(
      reset.ok() ? "Demo data reset." : "Not the Demo environment: keeping the current data.",
    );
    await api.post("/api/v1/auth/logout");
  } finally {
    await api.dispose();
  }
}
