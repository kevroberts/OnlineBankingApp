import { render, type RenderOptions } from "@testing-library/react";
import { NuqsTestingAdapter, type OnUrlUpdateFunction } from "nuqs/adapters/testing";
import type { ReactElement, ReactNode } from "react";
import { Provider } from "react-redux";
import { TooltipProvider } from "@/components/ui/tooltip";
import { makeStore, type RootState } from "@/store/store";

interface Options extends Omit<RenderOptions, "wrapper"> {
  preloadedState?: Partial<RootState>;
  /** Initial query string for nuqs, e.g. "?type=debit". */
  searchParams?: string;
  onUrlUpdate?: OnUrlUpdateFunction;
}

/** Renders with the real store (real reducers, listeners and RTK Query against MSW) and the app's providers. */
export function renderWithStore(
  ui: ReactElement,
  { preloadedState, searchParams, onUrlUpdate, ...options }: Options = {},
) {
  const store = makeStore(preloadedState);
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <Provider store={store}>
        <NuqsTestingAdapter searchParams={searchParams} onUrlUpdate={onUrlUpdate} hasMemory>
          <TooltipProvider>{children}</TooltipProvider>
        </NuqsTestingAdapter>
      </Provider>
    );
  }
  return { store, ...render(ui, { wrapper: Wrapper, ...options }) };
}

export const signedInState: Partial<RootState> = {
  session: {
    status: "signedIn",
    customer: {
      id: "a11ce000-0000-4000-8000-000000000001",
      displayName: "Alice Morgan",
      username: "alice",
    },
    endedBy: null,
  },
};
