import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { accountBalance, money, signIn } from "./helpers";

// The API limits requests per session, so these tests sign in with a session of their own
// instead of sharing the suite's.
test.use({ storageState: { cookies: [], origins: [] } });
test.beforeEach(async ({ page }) => signIn(page, "alice"));

async function openAssistant(page: Page) {
  await page.goto("/accounts");
  await page.getByRole("button", { name: "Ask" }).click();
  const panel = page.getByRole("dialog", { name: "Ask about your accounts" });
  await expect(panel.getByLabel("Your question")).toBeFocused();
  return panel;
}

async function ask(page: Page, question: string) {
  const panel = page.getByRole("dialog", { name: "Ask about your accounts" });
  // Direct children only: an answer can contain a list of its own.
  const messages = panel.locator('ol[aria-label="Conversation"] > li');
  const before = await messages.count();
  await panel.getByLabel("Your question").fill(question);
  await panel.getByLabel("Your question").press("Enter");
  // The question and its answer are added at once; then wait for the answer to finish streaming.
  await expect(messages).toHaveCount(before + 2);
  const answer = messages.last();
  await expect(answer.locator("[aria-busy]")).toHaveCount(0);
  return answer;
}

test("answers the three example questions from the customer's own data", async ({ page }) => {
  const checking = await accountBalance(page, "Everyday Checking");
  await openAssistant(page);

  const balance = await ask(page, "What is my checking balance?");
  await expect(balance).toContainText("Everyday Checking");
  expect(money(await balance.locator("strong").first().textContent())).toBe(checking);

  const groceries = await ask(page, "How much did I spend on groceries this month?");
  await expect(groceries).toContainText(/groceries/i);

  const recent = await ask(page, "Show my last five transactions.");
  await expect(recent.getByRole("listitem")).toHaveCount(5);
});

test("can't move money, and asking changes nothing", async ({ page }) => {
  const before = await accountBalance(page, "Everyday Checking");
  await openAssistant(page);
  const answer = await ask(page, "Send $500 to my savings account");
  await expect(answer).toContainText("can't move money");
  await page.keyboard.press("Escape");
  expect(await accountBalance(page, "Everyday Checking")).toBe(before);
});

test("New chat clears the conversation", async ({ page }) => {
  const panel = await openAssistant(page);
  await ask(page, "What is my checking balance?");
  await panel.getByRole("button", { name: "New chat" }).click();
  await expect(panel.getByRole("list", { name: "Conversation" })).toHaveCount(0);
  await expect(panel.getByRole("button", { name: "What is my checking balance?" })).toBeVisible();
});

test("no serious accessibility violations in the assistant panel", async ({ page }) => {
  await openAssistant(page);
  await ask(page, "Show my last five transactions.");
  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter(
    (v) => v.impact === "serious" || v.impact === "critical",
  );
  expect(
    serious.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`),
  ).toEqual([]);
});
