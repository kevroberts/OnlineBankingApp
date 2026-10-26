import {
  generatedApi,
  type ListTransactionsApiArg,
  type TransactionPageDto,
  type TransferPageDto,
} from "./bankApi.generated";
import type { BankExtraOptions } from "./baseApi";

export * from "./bankApi.generated";

export type HistoryFilters = Omit<ListTransactionsApiArg, "pageSize" | "cursor">;

export const HISTORY_PAGE_SIZE = 25;

const enhanced = generatedApi.enhanceEndpoints({
  endpoints: {
    listAccounts: {
      providesTags: (result) => [
        "Accounts",
        ...(result?.items.map((a) => ({ type: "Accounts" as const, id: a.id })) ?? []),
      ],
    },
    getAccount: {
      providesTags: (_r, _e, { accountId }) => [{ type: "Accounts", id: accountId }],
    },
    getCurrentCustomer: { providesTags: ["Me"] },
    listPayees: { providesTags: ["Payees"] },
    listTransfers: { providesTags: ["Transfers"] },
    getTransfer: {
      providesTags: (_r, _e, { transferId }) => [{ type: "Transfers", id: transferId }],
    },
    getTransaction: {
      providesTags: (_r, _e, { accountId }) => [{ type: "History", id: accountId }],
    },
    executeTransfer: {
      // Network errors, timeouts and 409 request_in_progress retry with the same key and body.
      extraOptions: { retry: true } satisfies BankExtraOptions,
      invalidatesTags: (result) =>
        result
          ? [
              "Accounts",
              "Transfers",
              { type: "History", id: result.source.id },
              { type: "History", id: result.destination.id },
            ]
          : [],
    },
  },
});

/**
 * Transaction history as an infinite query: the cache key is the filters, so a filter change
 * starts a fresh cursor chain, as the API requires.
 */
export const bankApi = enhanced.injectEndpoints({
  endpoints: (build) => ({
    listTransactionsInfinite: build.infiniteQuery<
      TransactionPageDto,
      HistoryFilters,
      string | null
    >({
      infiniteQueryOptions: {
        initialPageParam: null,
        getNextPageParam: (lastPage) =>
          lastPage.page.hasMore ? lastPage.page.nextCursor : undefined,
      },
      query: ({ queryArg, pageParam }) => ({
        url: `/api/v1/accounts/${queryArg.accountId}/transactions`,
        params: {
          from: queryArg.from,
          to: queryArg.to,
          type: queryArg.type,
          q: queryArg.q,
          pageSize: HISTORY_PAGE_SIZE,
          cursor: pageParam ?? undefined,
        },
      }),
      providesTags: (_r, _e, { accountId }) => [{ type: "History", id: accountId }],
    }),
    listTransfersInfinite: build.infiniteQuery<TransferPageDto, void, string | null>({
      infiniteQueryOptions: {
        initialPageParam: null,
        getNextPageParam: (lastPage) =>
          lastPage.page.hasMore ? lastPage.page.nextCursor : undefined,
      },
      query: ({ pageParam }) => ({
        url: `/api/v1/transfers`,
        params: { pageSize: HISTORY_PAGE_SIZE, cursor: pageParam ?? undefined },
      }),
      providesTags: ["Transfers"],
    }),
  }),
});

export const {
  useListAccountsQuery,
  useGetAccountQuery,
  useListTransactionsInfiniteInfiniteQuery: useListTransactionsInfiniteQuery,
  useGetCurrentCustomerQuery,
  useLoginMutation,
  useLogoutMutation,
  useListPayeesQuery,
  useCreateTransferQuoteMutation,
  useExecuteTransferMutation,
  useListTransfersQuery,
  useListTransfersInfiniteInfiniteQuery: useListTransfersInfiniteQuery,
  useGetTransferQuery,
} = bankApi;
