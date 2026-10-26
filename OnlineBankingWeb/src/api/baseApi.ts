import { createAction } from "@reduxjs/toolkit";
import {
  createApi,
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from "@reduxjs/toolkit/query/react";
import { toApiProblem, type ApiProblem } from "./problem";

/** Dispatched when the API says the session is gone (any 401) and on sign-out. */
export const sessionEnded = createAction<{ serverSessionCleared?: boolean } | undefined>(
  "session/ended",
);

const rawBaseQuery = fetchBaseQuery({
  // Same origin: Next.js rewrites /api/* to the API, and the browser sends obk_session itself.
  baseUrl: "",
  credentials: "same-origin",
});

export interface BankExtraOptions {
  /** Retry network errors, timeouts and 409 request_in_progress with the same arguments. */
  retry?: boolean;
}

const MAX_ATTEMPTS = 3;
const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

/** Endpoints whose 401 is an answer about credentials, not an expired session. */
const SESSIONLESS_ENDPOINTS = new Set(["login", "logout"]);

/**
 * fetchBaseQuery plus the app's rules: errors become a typed ApiProblem, a 401 ends the session,
 * and opted-in endpoints retry with identical arguments (so the same Idempotency-Key and body).
 */
export const bankBaseQuery: BaseQueryFn<
  string | FetchArgs,
  unknown,
  ApiProblem,
  BankExtraOptions
> = async (args, api, extraOptions) => {
  for (let attempt = 1; ; attempt++) {
    const result = await rawBaseQuery(args, api, {});
    if (!result.error) return { data: result.data, meta: result.meta };

    const problem = toApiProblem(
      result.error as FetchBaseQueryError,
      result.meta?.response?.headers.get("Retry-After"),
    );

    // Once signed out, further 401s (such as a refetch of /auth/me) need no second cleanup.
    const status = (api.getState() as { session?: { status?: string } }).session?.status;
    if (
      problem.status === 401 &&
      !SESSIONLESS_ENDPOINTS.has(api.endpoint) &&
      status !== "signedOut"
    ) {
      api.dispatch(sessionEnded());
    }

    const canRetry = extraOptions?.retry && attempt < MAX_ATTEMPTS && !api.signal.aborted;
    if (canRetry && problem.status === 0) {
      await sleep(500 * 2 ** (attempt - 1));
      continue;
    }
    if (canRetry && problem.status === 409 && problem.code === "request_in_progress") {
      await sleep(Math.min(problem.retryAfter ?? 1, 5) * 1000);
      continue;
    }
    return { error: problem, meta: result.meta };
  }
};

export const baseApi = createApi({
  reducerPath: "bankApi",
  baseQuery: bankBaseQuery,
  tagTypes: ["Accounts", "History", "Payees", "Transfers", "Me"],
  refetchOnFocus: true,
  endpoints: () => ({}),
});
