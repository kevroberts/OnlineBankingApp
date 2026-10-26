import { waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { sessionEnded } from "@/api/baseApi";
import { db, problem, sseResponse } from "../../../tests/mocks/handlers";
import { server } from "../../../tests/mocks/node";
import { signedInState } from "../../../tests/renderWithStore";
import { makeStore } from "@/store/store";
import { askAssistant, SseParser, startNewChat } from "./streamAssistant";

const CONVERSATION = "c0000000-0000-4000-8000-000000000001";

describe("SseParser", () => {
  it("returns whole events across chunk boundaries and ignores comments", () => {
    const parser = new SseParser();
    expect(parser.push('event: text\ndata: {"delta":"Hel')).toEqual([]);
    expect(parser.push('lo"}\n\n: keep-alive\n\nevent: done\r\ndata: {}\r\n\r\n')).toEqual([
      { event: "text", data: '{"delta":"Hello"}' },
      { event: "done", data: "{}" },
    ]);
  });

  it("joins multi-line data and defaults the event name", () => {
    expect(new SseParser().push("data: a\ndata: b\n\n")).toEqual([
      { event: "message", data: "a\nb" },
    ]);
  });
});

describe("askAssistant", () => {
  it("streams the answer into the chat and remembers the conversation", async () => {
    db.signedIn = true;
    const store = makeStore(signedInState);
    await store.dispatch(askAssistant("What is my checking balance?"));

    const { assistant } = store.getState();
    expect(assistant.mode).toBe("mock");
    expect(assistant.conversationId).toMatch(/^[0-9a-f-]{36}$/);
    expect(assistant.pendingReplyId).toBeNull();
    expect(assistant.messages.map((m) => [m.role, m.status, m.text])).toEqual([
      ["user", "done", "What is my checking balance?"],
      ["assistant", "done", "Your Everyday Checking (****4821) balance is **$2,650.75**."],
    ]);
  });

  it("sends the conversation id with a follow-up question", async () => {
    const bodies: unknown[] = [];
    server.use(
      http.post("*/api/v1/assistant/messages", async ({ request }) => {
        bodies.push(await request.json());
        return sseResponse([
          ["conversation", { conversationId: CONVERSATION, mode: "claude" }],
          ["text", { delta: "OK" }],
          ["done", { stopReason: "end_turn" }],
        ]);
      }),
    );
    const store = makeStore(signedInState);
    await store.dispatch(askAssistant("first"));
    await store.dispatch(askAssistant("second"));
    expect(bodies).toEqual([
      { conversationId: null, text: "first" },
      { conversationId: CONVERSATION, text: "second" },
    ]);
    expect(store.getState().assistant.mode).toBe("claude");
  });

  it("shows the error event in place of a partial answer", async () => {
    server.use(
      http.post("*/api/v1/assistant/messages", () =>
        sseResponse([
          ["conversation", { conversationId: CONVERSATION, mode: "claude" }],
          ["text", { delta: "Your balance is" }],
          ["error", { code: "assistant_unavailable", detail: "The assistant didn't answer." }],
        ]),
      ),
    );
    const store = makeStore(signedInState);
    await store.dispatch(askAssistant("balance?"));
    const reply = store.getState().assistant.messages.at(1);
    expect(reply).toMatchObject({
      status: "error",
      text: "The assistant didn't answer. Try again.",
    });
    expect(store.getState().assistant.conversationId).toBe(CONVERSATION);
  });

  it("starts a new conversation after the old one expired", async () => {
    server.use(
      http.post("*/api/v1/assistant/messages", () =>
        sseResponse([["error", { code: "conversation_expired", detail: "Expired." }]]),
      ),
    );
    const store = makeStore({
      ...signedInState,
      assistant: { conversationId: CONVERSATION, mode: "mock", messages: [], pendingReplyId: null },
    });
    await store.dispatch(askAssistant("still there?"));
    expect(store.getState().assistant.conversationId).toBeNull();
    expect(store.getState().assistant.messages.at(1)?.text).toMatch(/expired/);
  });

  it("fails the reply when the stream stops without done", async () => {
    server.use(
      http.post("*/api/v1/assistant/messages", () => sseResponse([["text", { delta: "Your" }]])),
    );
    const store = makeStore(signedInState);
    await store.dispatch(askAssistant("balance?"));
    expect(store.getState().assistant.messages.at(1)).toMatchObject({
      status: "error",
      text: "We couldn't reach the bank. Check your connection and try again.",
    });
  });

  it("shows the API's problem when the request is refused", async () => {
    server.use(
      http.post("*/api/v1/assistant/messages", () =>
        problem(400, "bad_request", "Ask a question."),
      ),
    );
    const store = makeStore(signedInState);
    await store.dispatch(askAssistant("x"));
    expect(store.getState().assistant.messages.at(1)?.text).toBe(
      "Something about that request wasn't right. Please try again.",
    );
  });

  it("ends the session on a 401 and clears the chat", async () => {
    db.signedIn = false;
    const store = makeStore(signedInState);
    await store.dispatch(askAssistant("balance?"));
    await waitFor(() => expect(store.getState().session.status).toBe("signedOut"));
    expect(store.getState().assistant.messages).toEqual([]);
  });

  it("drops an answer that arrives after sign-out", async () => {
    let release!: () => void;
    const gate = new Promise<void>((resolve) => (release = resolve));
    server.use(
      http.post("*/api/v1/assistant/messages", async () => {
        await gate;
        return sseResponse([
          ["text", { delta: "late" }],
          ["done", { stopReason: "end_turn" }],
        ]);
      }),
    );
    const store = makeStore(signedInState);
    const asked = store.dispatch(askAssistant("balance?"));
    store.dispatch(sessionEnded({ serverSessionCleared: true }));
    release();
    await asked;
    expect(store.getState().assistant.messages).toEqual([]);
  });
});

describe("startNewChat", () => {
  it("clears the chat and tells the API to forget it", async () => {
    db.signedIn = true;
    const deleted: string[] = [];
    server.use(
      http.delete("*/api/v1/assistant/conversations/:id", ({ params }) => {
        deleted.push(params.id as string);
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const store = makeStore(signedInState);
    await store.dispatch(askAssistant("What is my checking balance?"));
    const id = store.getState().assistant.conversationId;
    await store.dispatch(startNewChat());
    expect(deleted).toEqual([id]);
    expect(store.getState().assistant).toMatchObject({ conversationId: null, messages: [] });
  });
});
