import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { delay, http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { TransferQuoteDto } from "@/api/bankApi";
import { IDS } from "../../../tests/mocks/db";
import { db } from "../../../tests/mocks/handlers";
import { server } from "../../../tests/mocks/node";
import { renderWithStore, signedInState } from "../../../tests/renderWithStore";
import { mockRouter } from "../../../tests/setup";
import { ConfirmPanel } from "./ConfirmPanel";
import { emptyDraft } from "./transferDraftSlice";

const KEY = "5b1f7f0e-7d7e-4a0e-9f63-2a8c2b0d4c11";

function seedQuote(): TransferQuoteDto {
  const quote: TransferQuoteDto = {
    quoteId: "9d0b3c4e-1111-4222-8333-444455556666",
    source: {
      id: IDS.checking,
      name: "Everyday Checking",
      maskedNumber: "****4821",
      balance: 2650.75,
    },
    destination: {
      kind: "account",
      id: IDS.savings,
      name: "High-Yield Savings",
      maskedNumber: "****9137",
    },
    amount: 200,
    fee: 0,
    note: null,
    sourceBalanceAfter: 2450.75,
    expiresAt: new Date(Date.now() + 4 * 60_000).toISOString(),
  };
  db.signedIn = true;
  // A copy: the mock API marks its own quote as used, and must not touch the store's.
  db.quotes.set(quote.quoteId, { ...quote });
  return quote;
}

function renderConfirm(quote: TransferQuoteDto) {
  return renderWithStore(<ConfirmPanel />, {
    preloadedState: {
      ...signedInState,
      transferDraft: {
        ...emptyDraft,
        quote,
        idempotencyKey: KEY,
        form: {
          sourceAccountId: IDS.checking,
          destination: `account:${IDS.savings}`,
          amount: "200",
          note: "",
        },
      },
    },
  });
}

/** Records every execute request's Idempotency-Key and body; returning nothing falls through to the mock API. */
function recordExecutes(first?: () => Response | undefined | Promise<Response | undefined>) {
  const calls: { key: string | null; body: string }[] = [];
  server.use(
    http.post("*/api/v1/transfers", async ({ request }) => {
      calls.push({
        key: request.headers.get("Idempotency-Key"),
        body: await request.clone().text(),
      });
      if (calls.length === 1 && first) return first();
      return undefined;
    }),
  );
  return calls;
}

describe("confirmation step", () => {
  it("shows the quote, its expiry countdown, and moves focus to the heading", async () => {
    renderConfirm(seedQuote());
    const heading = await screen.findByRole("heading", { name: "Confirm your transfer" });
    await waitFor(() => expect(heading).toHaveFocus());
    expect(screen.getByText("$200.00")).toBeInTheDocument();
    expect(screen.getByText(/This quote expires in [34]:\d\d/)).toBeInTheDocument();
  });

  it("sends the draft's key, disables Confirm while pending, and shows the result", async () => {
    const calls = recordExecutes(async () => {
      await delay(150);
      return undefined;
    });
    const user = userEvent.setup();
    const { store } = renderConfirm(seedQuote());

    const confirm = await screen.findByRole("button", { name: "Confirm transfer" });
    await user.dblClick(confirm);
    expect(await screen.findByRole("button", { name: "Sending…" })).toBeDisabled();

    await waitFor(() =>
      expect(mockRouter.replace).toHaveBeenCalledWith(expect.stringMatching(/^\/transfers\/.+/)),
    );
    expect(calls).toHaveLength(1);
    expect(calls[0]!.key).toBe(KEY);
    expect(db.transfers).toHaveLength(1);
    expect(store.getState().transferDraft).toEqual(emptyDraft);
  });

  it("retries a network failure with the same key and body, so money moves once", async () => {
    const calls = recordExecutes(() => HttpResponse.error());
    const user = userEvent.setup();
    renderConfirm(seedQuote());

    await user.click(await screen.findByRole("button", { name: "Confirm transfer" }));

    await waitFor(
      () =>
        expect(mockRouter.replace).toHaveBeenCalledWith(expect.stringMatching(/^\/transfers\//)),
      { timeout: 3000 },
    );
    expect(calls).toHaveLength(2);
    expect(calls[1]).toEqual(calls[0]);
    expect(calls[0]!.key).toBe(KEY);
    expect(db.transfers).toHaveLength(1);
  });

  it("goes back to the form with the error on its field when funds ran out", async () => {
    const quote = seedQuote();
    db.accounts[0]!.balance = 10;
    server.use(
      http.post("*/api/v1/transfers", () =>
        HttpResponse.json(
          {
            status: 422,
            code: "validation_failed",
            errors: [{ field: "amount", code: "insufficient_funds", message: "x" }],
          },
          { status: 422, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );
    const user = userEvent.setup();
    const { store } = renderConfirm(quote);

    await user.click(await screen.findByRole("button", { name: "Confirm transfer" }));

    await waitFor(() => expect(mockRouter.replace).toHaveBeenCalledWith("/transfer"));
    const draft = store.getState().transferDraft;
    expect(draft.quote).toBeNull();
    expect(draft.form?.amount).toBe("200");
    expect(draft.errors).toEqual([{ field: "amount", code: "insufficient_funds", message: "x" }]);
  });
});
