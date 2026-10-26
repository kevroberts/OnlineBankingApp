"use client";

import { useEffect, useState, type ReactNode } from "react";

const ENABLED = process.env.NEXT_PUBLIC_API_MOCKING === "enabled";

/**
 * Offline demo mode: with NEXT_PUBLIC_API_MOCKING=enabled, the same MSW handlers the component tests use
 * answer /api/* in the browser, so the app runs without the API. Sign in as alice / Demo#2026.
 */
export function ApiMocking({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(!ENABLED);
  useEffect(() => {
    if (!ENABLED) return;
    void import("../../tests/mocks/browser").then(({ worker }) =>
      worker.start({ onUnhandledRequest: "bypass", quiet: true }).then(() => setReady(true)),
    );
  }, []);
  return ready ? <>{children}</> : null;
}
