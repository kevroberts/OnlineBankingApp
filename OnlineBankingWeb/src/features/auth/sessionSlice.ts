import { createSlice, type PayloadAction } from "@reduxjs/toolkit";
import { bankApi, type CustomerDto } from "@/api/bankApi";
import { sessionEnded } from "@/api/baseApi";

export type SessionStatus = "unknown" | "signedIn" | "signedOut";

export interface SessionState {
  status: SessionStatus;
  customer: CustomerDto | null;
  /** Why the session ended: the customer signed out, or the API stopped accepting it. */
  endedBy: "user" | "expired" | null;
}

const initialState: SessionState = { status: "unknown", customer: null, endedBy: null };

/** Mirrors the API's session: written only by RTK Query results and the 401 handling. */
export const sessionSlice = createSlice({
  name: "session",
  initialState,
  reducers: {
    signedIn(state, action: PayloadAction<CustomerDto>) {
      state.status = "signedIn";
      state.customer = action.payload;
      state.endedBy = null;
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(sessionEnded, (_state, action) => ({
        status: "signedOut",
        customer: null,
        endedBy: action.payload?.serverSessionCleared ? "user" : "expired",
      }))
      .addMatcher(bankApi.endpoints.getCurrentCustomer.matchFulfilled, (state, { payload }) => {
        state.status = "signedIn";
        state.customer = payload;
        state.endedBy = null;
      })
      .addMatcher(bankApi.endpoints.login.matchFulfilled, (state, { payload }) => {
        state.status = "signedIn";
        state.customer = payload.customer;
        state.endedBy = null;
      });
  },
  selectors: {
    selectSessionStatus: (state) => state.status,
    selectCustomer: (state) => state.customer,
    selectSessionEndedBy: (state) => state.endedBy,
  },
});

export const { signedIn } = sessionSlice.actions;
export const { selectSessionStatus, selectCustomer, selectSessionEndedBy } = sessionSlice.selectors;
