import { waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { bankApi } from "@/api/bankApi";
import { db } from "../../tests/mocks/handlers";
import { server } from "../../tests/mocks/node";
import { signedInState } from "../../tests/renderWithStore";
import { makeStore } from "./store";

describe("a 401 ends the session", () => {
  it("marks the session expired, clears cached data and the draft, and clears the cookie", async () => {
    db.signedIn = true;
    let logouts = 0;
    server.use(
      http.post("*/api/v1/auth/logout", () => {
        logouts++;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const store = makeStore({
      ...signedInState,
      transferDraft: { form: null, quote: null, idempotencyKey: "k", errors: [], notice: null },
    });
    await store.dispatch(bankApi.endpoints.listAccounts.initiate());
    expect(Object.keys(store.getState().bankApi.queries)).toHaveLength(1);

    db.signedIn = false; // the API's session timed out
    await store.dispatch(bankApi.endpoints.listPayees.initiate());

    await waitFor(() => expect(logouts).toBe(1));
    const state = store.getState();
    expect(state.session).toEqual({ status: "signedOut", customer: null, endedBy: "expired" });
    expect(state.bankApi.queries).toEqual({});
    expect(state.transferDraft.idempotencyKey).toBeNull();
  });

  it("does not treat a failed sign-in as an ended session", async () => {
    const store = makeStore();
    const result = await store.dispatch(
      bankApi.endpoints.login.initiate({ loginRequest: { username: "alice", password: "wrong" } }),
    );
    expect(result.error).toMatchObject({ status: 401, code: "invalid_credentials" });
    expect(store.getState().session.status).toBe("unknown");
  });
});
