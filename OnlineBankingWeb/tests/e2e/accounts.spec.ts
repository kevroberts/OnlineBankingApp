import { expect, test } from "@playwright/test";
import { ALICE } from "./helpers";

test("the overview shows both accounts with balances and a total", async ({ page }) => {
  await page.goto("/accounts");
  await expect(page.getByRole("heading", { name: "Your accounts" })).toBeVisible();
  const accounts = page.getByRole("list", { name: "Accounts" }).getByRole("listitem");
  await expect(accounts).toHaveCount(2);
  await expect(accounts.nth(0)).toContainText("Everyday Checking");
  await expect(accounts.nth(1)).toContainText("High-Yield Savings");
  await expect(page.getByText("Total balance")).toBeVisible();
});

test("history filters live in the URL and survive a refresh", async ({ page }) => {
  await page.goto(`/accounts/${ALICE.checking}`);
  const count = page.getByText(/^[\d,]+ results?$/);
  await expect(count).toBeVisible();
  const all = Number((await count.textContent())!.replace(/\D/g, ""));

  await page.getByRole("radio", { name: "Debits" }).click();
  await expect(page).toHaveURL(/type=debit/);
  await expect
    .poll(async () => Number((await count.textContent())!.replace(/\D/g, "")))
    .toBeLessThan(all);
  const table = page.getByRole("table");
  await expect(table.getByRole("row").nth(1)).toContainText("Debit");

  await page.getByLabel("Search").fill("coffee");
  await expect(page).toHaveURL(/q=coffee/);
  await expect(table.getByRole("row").nth(1)).toContainText(/coffee/i);

  await page.reload();
  await expect(page.getByLabel("Search")).toHaveValue("coffee");
  await expect(page.getByRole("radio", { name: "Debits" })).toHaveAttribute("aria-checked", "true");

  await page.getByRole("button", { name: "Clear filters" }).click();
  await expect(page).toHaveURL(new RegExp(`/accounts/${ALICE.checking}$`));
});

test("long histories load more pages with the running balance", async ({ page }) => {
  await page.goto(`/accounts/${ALICE.checking}`);
  const rows = page.getByRole("table").getByRole("row");
  await expect(rows).toHaveCount(26); // header + 25
  await page.getByRole("button", { name: "Load more" }).click();
  await expect.poll(() => rows.count()).toBeGreaterThan(26);
  await expect(page.getByRole("columnheader", { name: /Balance after/ })).toBeVisible();
});

test("another customer's account is not found", async ({ page }) => {
  await page.goto("/accounts/be000000-0000-4000-8000-0000000000a1");
  await expect(page.getByRole("heading", { name: "Not found" })).toBeVisible();
});
