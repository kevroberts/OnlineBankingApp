import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import { ALICE } from "./helpers";

const routes = ["/accounts", `/accounts/${ALICE.checking}`, "/transfer", "/transfers"];

async function expectNoSeriousViolations(page: import("@playwright/test").Page) {
  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === "serious" || v.impact === "critical",
  );
  expect(
    serious.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`),
  ).toEqual([]);
}

for (const route of routes) {
  test(`no serious accessibility violations on ${route}`, async ({ page }) => {
    await page.goto(route);
    await page.waitForLoadState("networkidle");
    await expect(page.getByRole("main")).not.toHaveAttribute("aria-busy", "true");
    await expectNoSeriousViolations(page);
  });
}

test("no serious accessibility violations on the confirmation and result screens", async ({
  page,
}) => {
  await page.goto(`/transfer?from=${ALICE.checking}`);
  await page.getByLabel("To").click();
  await page.getByRole("option", { name: /High-Yield Savings/ }).click();
  await page.getByLabel("Amount").fill("1");
  await page.getByRole("button", { name: "Review transfer" }).click();
  await expect(page.getByRole("button", { name: "Confirm transfer" })).toBeVisible();
  await expectNoSeriousViolations(page);
  await page.getByRole("button", { name: "Confirm transfer" }).click();
  await expect(page.getByRole("status")).toBeVisible();
  await expectNoSeriousViolations(page);
});

test.describe("signed out", () => {
  test.use({ storageState: { cookies: [], origins: [] } });
  test("no serious accessibility violations on /login", async ({ page }) => {
    await page.goto("/login");
    await expectNoSeriousViolations(page);
  });
});
