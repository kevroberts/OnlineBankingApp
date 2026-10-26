import { describe, expect, it } from "vitest";
import { sessionEnded } from "@/api/baseApi";
import { sessionSlice, signedIn } from "./sessionSlice";

const reducer = sessionSlice.reducer;
const alice = { id: "1", displayName: "Alice Morgan", username: "alice" };

describe("sessionSlice", () => {
  it("starts unknown", () => {
    expect(reducer(undefined, { type: "init" })).toEqual({
      status: "unknown",
      customer: null,
      endedBy: null,
    });
  });

  it("records the customer on sign-in", () => {
    expect(reducer(undefined, signedIn(alice))).toEqual({
      status: "signedIn",
      customer: alice,
      endedBy: null,
    });
  });

  it("tells a sign-out from an expired session", () => {
    const signedInState = reducer(undefined, signedIn(alice));
    expect(reducer(signedInState, sessionEnded({ serverSessionCleared: true }))).toEqual({
      status: "signedOut",
      customer: null,
      endedBy: "user",
    });
    expect(reducer(signedInState, sessionEnded())).toMatchObject({
      status: "signedOut",
      endedBy: "expired",
    });
  });
});
