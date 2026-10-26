import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { db } from "../../../tests/mocks/handlers";
import { renderWithStore, signedInState } from "../../../tests/renderWithStore";
import { AssistantMarkdown } from "./AssistantMarkdown";
import { AssistantPanel } from "./AssistantPanel";

describe("AssistantPanel", () => {
  it("opens from the Ask button with the question box focused and example questions", async () => {
    const user = userEvent.setup();
    renderWithStore(<AssistantPanel />, { preloadedState: signedInState });
    await user.click(screen.getByRole("button", { name: "Ask" }));

    const dialog = screen.getByRole("dialog", { name: "Ask about your accounts" });
    expect(within(dialog).getByText(/can't move money/)).toBeInTheDocument();
    expect(within(dialog).getByLabelText("Your question")).toHaveFocus();
    expect(
      within(dialog).getByRole("button", { name: "Show my last five transactions." }),
    ).toBeVisible();
  });

  it("answers an example question and labels demo answers", async () => {
    db.signedIn = true;
    const user = userEvent.setup();
    renderWithStore(<AssistantPanel />, { preloadedState: signedInState });
    await user.click(screen.getByRole("button", { name: "Ask" }));
    await user.click(screen.getByRole("button", { name: "What is my checking balance?" }));

    const conversation = await screen.findByRole("list", { name: "Conversation" });
    const answer = await within(conversation).findByText("$2,650.75");
    expect(answer.tagName).toBe("STRONG");
    // The masked number's asterisks are text, not bold markers.
    expect(conversation).toHaveTextContent(
      "Your Everyday Checking (****4821) balance is $2,650.75.",
    );
    expect(screen.getByText(/^Assistant: Your Everyday/)).toHaveTextContent(
      "Assistant: Your Everyday Checking (****4821) balance is $2,650.75.",
    );
    expect(screen.getByText("Demo answers")).toBeInTheDocument();
  });

  it("sends a typed question with Enter and clears the box; New chat starts over", async () => {
    db.signedIn = true;
    const user = userEvent.setup();
    renderWithStore(<AssistantPanel />, { preloadedState: signedInState });
    await user.click(screen.getByRole("button", { name: "Ask" }));
    const input = screen.getByLabelText("Your question");
    await user.type(input, "What are my balances?{Enter}");

    expect(input).toHaveValue("");
    const conversation = await screen.findByRole("list", { name: "Conversation" });
    expect(await within(conversation).findByText(/High-Yield Savings:/)).toBeInTheDocument();
    // The finished answer is announced once to screen readers.
    expect(screen.getByText(/^Assistant: Here are your balances/)).toHaveClass("sr-only");
    await user.click(screen.getByRole("button", { name: "New chat" }));
    expect(screen.queryByRole("list", { name: "Conversation" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "What is my checking balance?" })).toBeVisible();
  });

  it("closes with Escape", async () => {
    const user = userEvent.setup();
    renderWithStore(<AssistantPanel />, { preloadedState: signedInState });
    await user.click(screen.getByRole("button", { name: "Ask" }));
    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

describe("AssistantMarkdown", () => {
  it("renders bold, lists and paragraphs, and never HTML", () => {
    const { container } = renderWithStore(
      <AssistantMarkdown
        text={
          "Your last **two**:\n- Oct 24: ATM, -$413.42\n- Oct 20: Pay\n\n<img src=x onerror=alert(1)>"
        }
      />,
    );
    expect(container.querySelectorAll("li")).toHaveLength(2);
    expect(screen.getByText("two").tagName).toBe("STRONG");
    expect(container.querySelector("img")).toBeNull();
    expect(screen.getByText("<img src=x onerror=alert(1)>")).toBeInTheDocument();
  });
});
