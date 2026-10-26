import { createListenerMiddleware, isRejected } from "@reduxjs/toolkit";
import { toast } from "sonner";
import { bankApi } from "@/api/bankApi";
import { sessionEnded } from "@/api/baseApi";
import { isApiProblem } from "@/api/problem";
import { saveDraft } from "@/features/transfers/transferDraftSlice";
import { problemMessage } from "@/lib/problemMessages";
import type { AppDispatch, RootState } from "./store";

export const listener = createListenerMiddleware();
const startListening = listener.startListening.withTypes<RootState, AppDispatch>();

// Draft persistence: the draft and its Idempotency-Key survive a refresh, but not a closed tab.
startListening({
  predicate: (_action, current, previous) => current.transferDraft !== previous.transferDraft,
  effect: (_action, api) => saveDraft(api.getState().transferDraft),
});

// Session end: drop cached data and clear the HttpOnly cookie, which JavaScript cannot remove.
startListening({
  actionCreator: sessionEnded,
  effect: async (action, api) => {
    // Several requests can 401 at once; handle the last one only.
    api.cancelActiveListeners();
    await api.delay(0);
    api.dispatch(bankApi.util.resetApiState());
    if (!action.payload?.serverSessionCleared) {
      await api.dispatch(bankApi.endpoints.logout.initiate(undefined, { track: false }));
    }
  },
});

// Background refresh failures: the screen still shows its last data, so say so in a toast.
startListening({
  predicate: (action) => isRejected(action) && action.type.startsWith(`${bankApi.reducerPath}/`),
  effect: (action, api) => {
    const meta = (
      action as { meta?: { arg?: { type?: string; queryCacheKey?: string }; condition?: boolean } }
    ).meta;
    if (meta?.condition || meta?.arg?.type !== "query" || !meta.arg.queryCacheKey) return;
    const payload = (action as { payload?: unknown }).payload;
    if (!isApiProblem(payload) || payload.status === 401) return;
    const before = api.getOriginalState().bankApi.queries[meta.arg.queryCacheKey];
    if (before?.data === undefined) return;
    toast.error("Couldn't refresh", { description: problemMessage(payload), id: "refresh-failed" });
  },
});
