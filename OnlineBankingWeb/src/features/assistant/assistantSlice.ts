import { createSlice, type PayloadAction } from "@reduxjs/toolkit";
import { sessionEnded } from "@/api/baseApi";

export type AssistantMode = "claude" | "mock";

export interface AssistantMessage {
  id: string;
  role: "user" | "assistant";
  text: string;
  status: "streaming" | "done" | "error";
  /** The tool the assistant is using while it streams, such as "list_accounts". */
  activity: string | null;
}

/** One chat at a time, in memory only: it is not persisted and is dropped on sign-out. */
export interface AssistantState {
  conversationId: string | null;
  mode: AssistantMode | null;
  messages: AssistantMessage[];
  /** The assistant message being streamed, or null when the assistant is idle. */
  pendingReplyId: string | null;
}

export const initialAssistantState: AssistantState = {
  conversationId: null,
  mode: null,
  messages: [],
  pendingReplyId: null,
};

function pending(state: AssistantState) {
  return state.messages.find((m) => m.id === state.pendingReplyId);
}

export const assistantSlice = createSlice({
  name: "assistant",
  initialState: initialAssistantState,
  reducers: {
    questionAsked(
      state,
      action: PayloadAction<{ questionId: string; replyId: string; text: string }>,
    ) {
      const { questionId, replyId, text } = action.payload;
      state.messages.push(
        { id: questionId, role: "user", text, status: "done", activity: null },
        { id: replyId, role: "assistant", text: "", status: "streaming", activity: null },
      );
      state.pendingReplyId = replyId;
    },
    conversationStarted(
      state,
      action: PayloadAction<{ conversationId: string; mode: AssistantMode }>,
    ) {
      state.conversationId = action.payload.conversationId;
      state.mode = action.payload.mode;
    },
    toolStarted(state, action: PayloadAction<{ tool: string }>) {
      const reply = pending(state);
      if (reply) reply.activity = action.payload.tool;
    },
    textReceived(state, action: PayloadAction<{ delta: string }>) {
      const reply = pending(state);
      if (reply) {
        reply.text += action.payload.delta;
        reply.activity = null;
      }
    },
    replyFinished(state) {
      const reply = pending(state);
      if (reply) {
        reply.status = "done";
        reply.activity = null;
      }
      state.pendingReplyId = null;
    },
    replyFailed(state, action: PayloadAction<{ message: string; conversationExpired?: boolean }>) {
      const reply = pending(state);
      if (reply) {
        // The API discards a failed turn, so a partial answer would not match what it remembers.
        reply.text = action.payload.message;
        reply.status = "error";
        reply.activity = null;
      }
      state.pendingReplyId = null;
      if (action.payload.conversationExpired) state.conversationId = null;
    },
    chatCleared() {
      return initialAssistantState;
    },
  },
  extraReducers: (builder) => {
    builder.addCase(sessionEnded, () => initialAssistantState);
  },
  selectors: {
    selectAssistant: (state) => state,
    selectIsAnswering: (state) => state.pendingReplyId !== null,
  },
});

export const {
  questionAsked,
  conversationStarted,
  toolStarted,
  textReceived,
  replyFinished,
  replyFailed,
  chatCleared,
} = assistantSlice.actions;

export const { selectAssistant, selectIsAnswering } = assistantSlice.selectors;
