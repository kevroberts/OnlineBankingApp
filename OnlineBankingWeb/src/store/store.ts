import { combineReducers, configureStore } from "@reduxjs/toolkit";
import { setupListeners } from "@reduxjs/toolkit/query";
import { bankApi } from "@/api/bankApi";
import { assistantSlice } from "@/features/assistant/assistantSlice";
import { sessionSlice } from "@/features/auth/sessionSlice";
import { transferDraftSlice } from "@/features/transfers/transferDraftSlice";
import { listener } from "./listeners";

const rootReducer = combineReducers({
  [bankApi.reducerPath]: bankApi.reducer,
  session: sessionSlice.reducer,
  transferDraft: transferDraftSlice.reducer,
  assistant: assistantSlice.reducer,
});

export type RootState = ReturnType<typeof rootReducer>;

export const makeStore = (preloadedState?: Partial<RootState>) => {
  const store = configureStore({
    reducer: rootReducer,
    preloadedState,
    middleware: (getDefault) =>
      getDefault().prepend(listener.middleware).concat(bankApi.middleware),
  });
  // refetchOnFocus / refetchOnReconnect
  setupListeners(store.dispatch);
  return store;
};

export type AppStore = ReturnType<typeof makeStore>;
export type AppDispatch = AppStore["dispatch"];
