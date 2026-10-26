import { expect, test } from "@playwright/test";
import { signIn } from "./helpers";

test.describe("signed out", () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  test("protected screens redirect to sign-in and come back after", async ({ page }) => {
    await page.goto("/accounts?from=x");
    await expect(page).toHaveURL(/\/login\?next=%2Faccounts%3Ffrom%3Dx$/);

    await page.getByLabel("Username").fill("chloe");
    await page.getByLabel("Password").fill("Demo#2026");
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page).toHaveURL(/\/accounts\?from=x$/);
    await expect(page.getByRole("heading", { name: "Your accounts" })).toBeVisible();
  });

  test("wrong credentials show one generic message", async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel("Username").fill("chloe");
    await page.getByLabel("Password").fill("not-the-password");
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page.getByRole("alert").filter({ hasText: "don't match" })).toHaveText(
      "That username and password don't match. Check them and try again.",
    );
    await expect(page).toHaveURL(/\/login$/);
  });

  test("a stale cookie passes the edge check, then lands on sign-in without a loop", async ({
    page,
    context,
    baseURL,
  }) => {
    await context.addCookies([
      { name: "obk_session", value: "stale", url: baseURL!, httpOnly: true, sameSite: "Strict" },
    ]);
    await page.goto("/accounts");
    await expect(page).toHaveURL(/\/login\?next=%2Faccounts$/);
    await expect(page.getByRole("button", { name: "Sign in" })).toBeVisible();
    // The listener cleared the stale cookie, so the next protected page fails the edge check cleanly.
    await expect
      .poll(async () => (await context.cookies()).some((c) => c.name === "obk_session"))
      .toBe(false);
  });

  test("signing out ends the session and blocks /accounts", async ({ page }) => {
    await signIn(page, "ben");
    await page.getByRole("button", { name: "Sign out" }).click();
    await expect(page).toHaveURL(/\/login$/);
    await page.goto("/accounts");
    await expect(page).toHaveURL(/\/login\?next=%2Faccounts$/);
  });
});
