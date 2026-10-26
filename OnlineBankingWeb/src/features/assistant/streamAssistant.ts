import { nanoid } from "@reduxjs/toolkit";
import { bankApi, type AssistantMessageRequest } from "@/api/bankApi";
import { sessionEnded } from "@/api/baseApi";
import { NETWORK_ERROR, toApiProblem } from "@/api/problem";
import { problemMessage } from "@/lib/problemMessages";
import type { AppDispatch, RootState } from "@/store/store";
import {
  chatCleared,
  conversationStarted,
  questionAsked,
  replyFailed,
  replyFinished,
  textReceived,
  toolStarted,
  type AssistantMode,
} from "./assistantSlice";

export interface SseEvent {
  event: string;
  data: string;
}

/** Incremental text/event-stream parser: feed it chunks as they arrive, get back whole events. */
export class SseParser {
  private buffer = "";

  push(chunk: string): SseEvent[] {
    this.buffer += chunk.replace(/\r\n?/g, "\n");
    const events: SseEvent[] = [];
    let end: number;
    while ((end = this.buffer.indexOf("\n\n")) !== -1) {
      const block = this.buffer.slice(0, end);
      this.buffer = this.buffer.slice(end + 2);
      let event = "message";
      const data: string[] = [];
      for (const line of block.split("\n")) {
        if (line.startsWith(":")) continue;
        const colon = line.indexOf(":");
        const field = colon === -1 ? line : line.slice(0, colon);
        const value = colon === -1 ? "" : line.slice(colon + 1).replace(/^ /, "");
        if (field === "event") event = value;
        else if (field === "data") data.push(value);
      }
      if (data.length > 0) events.push({ event, data: data.join("\n") });
    }
    return events;
  }
}

const UNAVAILABLE = "The assistant didn't answer. Try again.";

/** Customer-facing copy for the assistant's error codes; the API's detail is already written for customers. */
const ERROR_MESSAGES: Record<string, string> = {
  conversation_expired: "This chat has expired. Your next question starts a new one.",
  assistant_unavailable: UNAVAILABLE,
  assistant_declined: "The assistant can't help with that request.",
};

/** The data of every event the endpoint sends; each event uses some of these fields. */
interface StreamPayload {
  conversationId?: string;
  mode?: AssistantMode;
  tool?: string;
  delta?: string;
  stopReason?: string;
  code?: string;
  detail?: string;
}

type Thunk<T = void> = (dispatch: AppDispatch, getState: () => RootState) => Promise<T>;

/**
 * Asks the assistant a question and streams its answer into the assistant slice. The answer is
 * Server-Sent Events, which RTK Query does not read, so this uses fetch directly with the same
 * same-origin cookie and the same 401 rule as every other request.
 */
export function askAssistant(text: string): Thunk {
  return async (dispatch, getState) => {
    if (getState().assistant.pendingReplyId) return;
    const replyId = nanoid();
    dispatch(questionAsked({ questionId: nanoid(), replyId, text }));
    // A reset (new chat or sign-out) while streaming makes this reply stale.
    const stale = () => getState().assistant.pendingReplyId !== replyId;

    const body: AssistantMessageRequest = {
      conversationId: getState().assistant.conversationId,
      text,
    };

    let response: Response;
    try {
      response = await fetch("/api/v1/assistant/messages", {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
        body: JSON.stringify(body),
      });
    } catch {
      if (!stale()) dispatch(replyFailed({ message: problemMessage(networkProblem()) }));
      return;
    }

    if (stale()) {
      void response.body?.cancel().catch(() => undefined);
      return;
    }

    if (!response.ok || !response.body) {
      const data: unknown = await response.json().catch(() => undefined);
      const problem = toApiProblem({ status: response.status, data });
      if (problem.status === 401) {
        // sessionEnded also clears the chat; if the session had already ended, clear it here.
        if (getState().session.status !== "signedOut") dispatch(sessionEnded());
        else dispatch(chatCleared());
        return;
      }
      if (!stale()) dispatch(replyFailed({ message: problemMessage(problem) }));
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    const parser = new SseParser();
    let finished = false;
    try {
      while (!finished) {
        const { value, done } = await reader.read();
        if (done) break;
        if (stale()) {
          void reader.cancel().catch(() => undefined);
          return;
        }
        for (const { event, data } of parser.push(decoder.decode(value, { stream: true }))) {
          const payload = JSON.parse(data) as StreamPayload;
          switch (event) {
            case "conversation":
              dispatch(
                conversationStarted({
                  conversationId: payload.conversationId ?? "",
                  mode: payload.mode ?? "claude",
                }),
              );
              break;
            case "status":
              dispatch(toolStarted({ tool: payload.tool ?? "" }));
              break;
            case "text":
              dispatch(textReceived({ delta: payload.delta ?? "" }));
              break;
            case "done":
              dispatch(replyFinished());
              finished = true;
              break;
            case "error":
              dispatch(
                replyFailed({
                  message:
                    (payload.code && ERROR_MESSAGES[payload.code]) ?? payload.detail ?? UNAVAILABLE,
                  conversationExpired: payload.code === "conversation_expired",
                }),
              );
              finished = true;
              break;
          }
        }
      }
    } catch {
      if (!stale()) dispatch(replyFailed({ message: problemMessage(networkProblem()) }));
      return;
    }
    // The stream ended without done or error: the connection dropped mid-answer.
    if (!finished && !stale()) dispatch(replyFailed({ message: problemMessage(networkProblem()) }));
  };
}

/** Starts over: the panel empties at once, and the API forgets the old chat in the background. */
export function startNewChat(): Thunk {
  return async (dispatch, getState) => {
    const { conversationId } = getState().assistant;
    dispatch(chatCleared());
    if (conversationId) {
      // A 404 (already expired) is fine: either way the chat is gone.
      await dispatch(
        bankApi.endpoints.endAssistantConversation.initiate({ conversationId }, { track: false }),
      );
    }
  };
}

function networkProblem() {
  return { status: 0, code: NETWORK_ERROR, errors: [] };
}
