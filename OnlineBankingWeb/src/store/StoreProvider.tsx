"use client";

import { useState, type ReactNode } from "react";
import { Provider } from "react-redux";
import { loadDraft } from "@/features/transfers/transferDraftSlice";
import { makeStore } from "./store";

/**
 * One store per browser tab, created once (a lazy useState initializer, the lint-clean form of the
 * useRef pattern). A module-level store would be shared between requests on the server.
 */
export function StoreProvider({ children }: { children: ReactNode }) {
  const [store] = useState(() => {
    const draft = typeof window === "undefined" ? undefined : loadDraft();
    return makeStore(draft ? { transferDraft: draft } : undefined);
  });
  return <Provider store={store}>{children}</Provider>;
}
