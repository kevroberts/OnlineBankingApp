import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { IDS } from "../../../tests/mocks/db";
import { db } from "../../../tests/mocks/handlers";
import { renderWithStore, signedInState } from "../../../tests/renderWithStore";
import { HistoryFilters } from "./HistoryFilters";
import { HistoryList } from "./HistoryList";

function renderHistory(searchParams = "") {
  db.signedIn = true;
  const onUrlUpdate = vi.fn();
  const view = renderWithStore(
    <>
      <HistoryFilters />
      <HistoryList accountId={IDS.checking} currency="USD" />
    </>,
    { preloadedState: signedInState, searchParams, onUrlUpdate },
  );
  return { ...view, onUrlUpdate };
}

const table = () => screen.getByRole("table");
const rowCount = () => within(table()).getAllByRole("row").length - 1; // minus the header row

describe("transaction history", () => {
  it("lists transactions newest first with type, signed amount and balance after", async () => {
    renderHistory();
    expect(await screen.findByText("5 results")).toBeInTheDocument();
    const rows = within(table()).getAllByRole("row");
    expect(rows[1]).toHaveTextContent("ATM withdrawal");
    expect(rows[1]).toHaveTextContent("−$413.42");
    expect(rows[1]).toHaveTextContent("Debit");
    expect(rows[1]).toHaveTextContent("$2,650.75");
  });

  it("puts the type filter in the URL and queries with it", async () => {
    const user = userEvent.setup();
    const { onUrlUpdate } = renderHistory();
    await screen.findByText("5 results");

    await user.click(screen.getByRole("radio", { name: "Debits" }));

    await waitFor(() =>
      expect(onUrlUpdate).toHaveBeenLastCalledWith(
        expect.objectContaining({ queryString: "?type=debit" }),
      ),
    );
    expect(await screen.findByText("3 results")).toBeInTheDocument();
    expect(rowCount()).toBe(3);
  });

  it("debounces search and sends it only at 2 or more characters", async () => {
    const user = userEvent.setup();
    const { onUrlUpdate } = renderHistory("?type=debit");
    await screen.findByText("3 results");

    await user.type(screen.getByLabelText("Search"), "c");
    await new Promise((r) => setTimeout(r, 400));
    expect(onUrlUpdate).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText("Search"), "offee");
    await waitFor(() =>
      expect(onUrlUpdate).toHaveBeenLastCalledWith(
        expect.objectContaining({ queryString: "?type=debit&q=coffee" }),
      ),
    );
    expect(await screen.findByText("2 results")).toBeInTheDocument();
  });

  it("shows active filters as chips and clears them", async () => {
    const user = userEvent.setup();
    const { onUrlUpdate } = renderHistory("?type=credit&q=zzz");
    expect(await screen.findByText("No transactions match these filters")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Clear filters" }));
    await waitFor(() =>
      expect(onUrlUpdate).toHaveBeenLastCalledWith(expect.objectContaining({ queryString: "" })),
    );
    expect(await screen.findByText("5 results")).toBeInTheDocument();
  });
});
