import { expect, type Page } from "@playwright/test";

export const ALICE = {
  checking: "a11ce000-0000-4000-8000-0000000000a1",
  savings: "a11ce000-0000-4000-8000-0000000000a2",
};

/** Reads a currency amount such as "$2,650.75" as a number. */
export function money(text: string | null): number {
  return Number((text ?? "").replace(/[^0-9.\-−]/g, "").replace("−", "-"));
}

export async function accountBalance(page: Page, name: string): Promise<number> {
  await page.goto("/accounts");
  const card = page
    .getByRole("listitem")
    .filter({ has: page.getByRole("link", { name, exact: true }) });
  await expect(card).toBeVisible();
  return money(await card.getByText(/^\$[\d,]+\.\d\d$/).textContent());
}

export async function signIn(page: Page, username: string) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill("Demo#2026");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Your accounts" })).toBeVisible();
}
