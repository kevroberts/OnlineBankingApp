import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http } from "msw";
import { describe, expect, it } from "vitest";
import { IDS } from "../../../tests/mocks/db";
import { db, problem } from "../../../tests/mocks/handlers";
import { server } from "../../../tests/mocks/node";
import { renderWithStore, signedInState } from "../../../tests/renderWithStore";
import { mockRouter } from "../../../tests/setup";
import { emptyDraft } from "./transferDraftSlice";
import { TransferForm } from "./TransferForm";

const prefilled = {
  ...signedInState,
  transferDraft: {
    ...emptyDraft,
    form: {
      sourceAccountId: IDS.checking,
      destination: `account:${IDS.savings}`,
      amount: "100",
      note: "",
    },
  },
};

function describedBy(input: HTMLElement) {
  return (input.getAttribute("aria-describedby") ?? "")
    .split(" ")
    .map((id) => document.getElementById(id)?.textContent)
    .join(" ");
}

describe("transfer form", () => {
  it("creates a quote and moves to the confirmation step", async () => {
    db.signedIn = true;
    const user = userEvent.setup();
    const { store } = renderWithStore(<TransferForm />, { preloadedState: prefilled });

    await user.click(await screen.findByRole("button", { name: "Review transfer" }));

    await screen.findByRole("button", { name: "Review transfer" });
    expect(mockRouter.push).toHaveBeenCalledWith("/transfer/confirm");
    const draft = store.getState().transferDraft;
    expect(draft.quote?.amount).toBe(100);
    expect(draft.idempotencyKey).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("puts the API's field errors on the right inputs", async () => {
    db.signedIn = true;
    server.use(
      http.post("*/api/v1/transfers/quotes", () =>
        problem(422, "validation_failed", "The transfer failed one or more checks.", {
          errors: [
            { field: "amount", code: "insufficient_funds", message: "x" },
            { field: "destination.id", code: "account_not_active", message: "y" },
          ],
        }),
      ),
    );
    const user = userEvent.setup();
    renderWithStore(<TransferForm />, { preloadedState: prefilled });

    await user.click(await screen.findByRole("button", { name: "Review transfer" }));

    const amount = await screen.findByLabelText("Amount");
    await screen.findByText("The amount is more than the available balance.");
    expect(amount).toHaveAttribute("aria-invalid", "true");
    expect(describedBy(amount)).toContain("The amount is more than the available balance.");
    expect(describedBy(screen.getByLabelText("To"))).toContain(
      "This account can't be used for transfers right now.",
    );
    expect(mockRouter.push).not.toHaveBeenCalled();
  });

  it("checks the rules before calling the API", async () => {
    db.signedIn = true;
    const user = userEvent.setup();
    renderWithStore(<TransferForm />, { preloadedState: prefilled });

    const amount = await screen.findByLabelText("Amount");
    await user.clear(amount);
    await user.type(amount, "5000");
    await user.click(screen.getByRole("button", { name: "Review transfer" }));

    expect(
      await screen.findByText("The amount is more than the available balance."),
    ).toBeInTheDocument();
    expect(db.quotes.size).toBe(0);
  });
});
