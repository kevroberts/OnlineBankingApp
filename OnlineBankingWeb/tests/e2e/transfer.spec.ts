import { expect, test, type Page } from "@playwright/test";
import { accountBalance, ALICE, money } from "./helpers";

test.describe.configure({ mode: "serial" });

async function fillTransfer(page: Page, amount: string, note: string) {
  await page.goto(`/transfer?from=${ALICE.checking}`);
  await page.getByLabel("To").click();
  await page.getByRole("option", { name: /High-Yield Savings/ }).click();
  await page.getByLabel("Amount").fill(amount);
  await page.getByLabel("Note (optional)").fill(note);
  await page.getByRole("button", { name: "Review transfer" }).click();
  await expect(page).toHaveURL(/\/transfer\/confirm$/);
  await expect(page.getByRole("heading", { name: "Confirm your transfer" })).toBeFocused();
}

test("invalid transfers are stopped before the confirmation step", async ({ page }) => {
  await page.goto(`/transfer?from=${ALICE.checking}`);
  await page.getByLabel("Amount").fill("0");
  await page.getByRole("button", { name: "Review transfer" }).click();
  await expect(page.getByText("Choose where the money should go.")).toBeVisible();
  await expect(page.getByText("Enter an amount greater than zero.")).toBeVisible();

  await page.getByLabel("To").click();
  await page.getByRole("option", { name: /High-Yield Savings/ }).click();
  await page.getByLabel("Amount").fill("999999");
  await page.getByRole("button", { name: "Review transfer" }).click();
  await expect(page.getByText("The amount is more than the available balance.")).toBeVisible();
  await expect(page).toHaveURL(/\/transfer\?from=/);
});

test("transfer 200.00 to savings: balances and both histories update, once", async ({ page }) => {
  const checkingBefore = await accountBalance(page, "Everyday Checking");
  const savingsBefore = await accountBalance(page, "High-Yield Savings");
  const note = `E2E ${Date.now()}`;

  await fillTransfer(page, "200", note);
  await expect(page.getByText("$200.00", { exact: true })).toBeVisible();

  // Refresh on the confirmation step keeps the draft and its Idempotency-Key.
  await page.reload();
  await expect(page.getByRole("button", { name: "Confirm transfer" })).toBeVisible();
  await page.getByRole("button", { name: "Confirm transfer" }).dblclick();

  await expect(page).toHaveURL(/\/transfers\/[0-9a-f-]{36}$/);
  await expect(page.getByRole("status")).toContainText("$200.00 sent to High-Yield Savings");
  await expect(page.getByText(/^TRF-\d{8}-\d{6}$/)).toBeVisible();

  // Back does not return to the confirmation screen.
  await page.goBack();
  await expect(page).not.toHaveURL(/\/transfer\/confirm$/);

  expect(await accountBalance(page, "Everyday Checking")).toBeCloseTo(checkingBefore - 200, 2);
  expect(await accountBalance(page, "High-Yield Savings")).toBeCloseTo(savingsBefore + 200, 2);

  for (const [account, sign] of [
    [ALICE.checking, "−"],
    [ALICE.savings, "+"],
  ] as const) {
    await page.goto(`/accounts/${account}?q=${encodeURIComponent(note)}`);
    const rows = page.getByRole("table").getByRole("row");
    await expect(rows).toHaveCount(2); // header + exactly one entry: the money moved once
    await expect(rows.nth(1)).toContainText(`${sign}$200.00`);
  }
});

test("replaying a confirmed transfer after a refresh does not move money again", async ({
  page,
}) => {
  const before = await accountBalance(page, "Everyday Checking");
  await fillTransfer(page, "1.25", `E2E replay ${Date.now()}`);

  // Keep a copy of the draft (quote and key) as it was before confirming.
  const draft = await page.evaluate(() => sessionStorage.getItem("obk.transfer"));
  await page.getByRole("button", { name: "Confirm transfer" }).click();
  await expect(page).toHaveURL(/\/transfers\//);
  const firstResult = page.url();

  // Put the same draft back, as if the first response had been lost, and confirm again.
  await page.evaluate((d) => sessionStorage.setItem("obk.transfer", d!), draft);
  await page.goto("/transfer/confirm");
  await page.getByRole("button", { name: "Confirm transfer" }).click();
  await expect(page).toHaveURL(firstResult);

  expect(await accountBalance(page, "Everyday Checking")).toBeCloseTo(before - 1.25, 2);
});

test("recent transfers lists the new transfer", async ({ page }) => {
  await page.goto("/transfers");
  const first = page.getByRole("list", { name: "Recent transfers" }).getByRole("listitem").first();
  await expect(first).toContainText("Everyday Checking → High-Yield Savings");
  expect(money(await first.getByText(/^\$/).textContent())).toBeGreaterThan(0);
});
