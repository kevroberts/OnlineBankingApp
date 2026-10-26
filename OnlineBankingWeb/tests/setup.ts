import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll, vi } from "vitest";
import { resetDb } from "./mocks/handlers";
import { server } from "./mocks/node";

// Node's Request rejects relative URLs; the browser resolves them against the page. Do the same here,
// so fetchBaseQuery's same-origin "/api/..." URLs work in tests exactly as in the app.
const NodeRequest = globalThis.Request;
globalThis.Request = class extends NodeRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    super(
      typeof input === "string" && input.startsWith("/")
        ? new URL(input, window.location.origin)
        : input,
      init,
    );
  }
} as typeof Request;

// Radix and the history list use browser APIs that jsdom lacks.
class NoopObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
  takeRecords() {
    return [];
  }
}
globalThis.ResizeObserver ??= NoopObserver as unknown as typeof ResizeObserver;
globalThis.IntersectionObserver ??= NoopObserver as unknown as typeof IntersectionObserver;
Element.prototype.scrollIntoView ??= vi.fn();
Element.prototype.hasPointerCapture ??= () => false;
Element.prototype.releasePointerCapture ??= vi.fn();

const router = {
  push: vi.fn(),
  replace: vi.fn(),
  back: vi.fn(),
  prefetch: vi.fn(),
  refresh: vi.fn(),
  forward: vi.fn(),
};
export const mockRouter = router;
export const mockNavigation = {
  pathname: "/",
  params: {} as Record<string, string>,
  search: new URLSearchParams(),
};

vi.mock("next/navigation", () => ({
  useRouter: () => router,
  usePathname: () => mockNavigation.pathname,
  useParams: () => mockNavigation.params,
  useSearchParams: () => mockNavigation.search,
  notFound: () => {
    throw new Error("NEXT_NOT_FOUND");
  },
  redirect: vi.fn(),
}));

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
  resetDb();
  sessionStorage.clear();
  vi.clearAllMocks();
  mockNavigation.pathname = "/";
  mockNavigation.params = {};
  mockNavigation.search = new URLSearchParams();
});
afterAll(() => server.close());
