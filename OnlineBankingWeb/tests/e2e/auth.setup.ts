import { expect, test as setup } from "@playwright/test";

// One sign-in for the whole suite: the API allows 10 sign-ins per minute per IP.
setup("sign in as alice", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Username").fill("alice");
  await page.getByLabel("Password").fill("Demo#2026");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Your accounts" })).toBeVisible();
  await page.context().storageState({ path: "tests/e2e/.auth/alice.json" });
});
