import { baseApi as api } from "./baseApi";
const injectedRtkApi = api.injectEndpoints({
  endpoints: (build) => ({
    listAccounts: build.query<ListAccountsApiResponse, ListAccountsApiArg>({
      query: () => ({ url: `/api/v1/accounts` }),
    }),
    getAccount: build.query<GetAccountApiResponse, GetAccountApiArg>({
      query: (queryArg) => ({ url: `/api/v1/accounts/${queryArg.accountId}` }),
    }),
    listTransactions: build.query<
      ListTransactionsApiResponse,
      ListTransactionsApiArg
    >({
      query: (queryArg) => ({
        url: `/api/v1/accounts/${queryArg.accountId}/transactions`,
        params: {
          from: queryArg["from"],
          to: queryArg.to,
          type: queryArg["type"],
          q: queryArg.q,
          pageSize: queryArg.pageSize,
          cursor: queryArg.cursor,
        },
      }),
    }),
    getTransaction: build.query<
      GetTransactionApiResponse,
      GetTransactionApiArg
    >({
      query: (queryArg) => ({
        url: `/api/v1/accounts/${queryArg.accountId}/transactions/${queryArg.transactionId}`,
      }),
    }),
    askAssistant: build.mutation<AskAssistantApiResponse, AskAssistantApiArg>({
      query: (queryArg) => ({
        url: `/api/v1/assistant/messages`,
        method: "POST",
        body: queryArg.assistantMessageRequest,
      }),
    }),
    endAssistantConversation: build.mutation<
      EndAssistantConversationApiResponse,
      EndAssistantConversationApiArg
    >({
      query: (queryArg) => ({
        url: `/api/v1/assistant/conversations/${queryArg.conversationId}`,
        method: "DELETE",
      }),
    }),
    login: build.mutation<LoginApiResponse, LoginApiArg>({
      query: (queryArg) => ({
        url: `/api/v1/auth/login`,
        method: "POST",
        body: queryArg.loginRequest,
      }),
    }),
    logout: build.mutation<LogoutApiResponse, LogoutApiArg>({
      query: () => ({ url: `/api/v1/auth/logout`, method: "POST" }),
    }),
    getCurrentCustomer: build.query<
      GetCurrentCustomerApiResponse,
      GetCurrentCustomerApiArg
    >({
      query: () => ({ url: `/api/v1/auth/me` }),
    }),
    listPayees: build.query<ListPayeesApiResponse, ListPayeesApiArg>({
      query: () => ({ url: `/api/v1/payees` }),
    }),
    createTransferQuote: build.mutation<
      CreateTransferQuoteApiResponse,
      CreateTransferQuoteApiArg
    >({
      query: (queryArg) => ({
        url: `/api/v1/transfers/quotes`,
        method: "POST",
        body: queryArg.transferQuoteRequest,
      }),
    }),
    executeTransfer: build.mutation<
      ExecuteTransferApiResponse,
      ExecuteTransferApiArg
    >({
      query: (queryArg) => ({
        url: `/api/v1/transfers`,
        method: "POST",
        body: queryArg.executeTransferRequest,
        headers: {
          "Idempotency-Key": queryArg["Idempotency-Key"],
        },
      }),
    }),
    listTransfers: build.query<ListTransfersApiResponse, ListTransfersApiArg>({
      query: (queryArg) => ({
        url: `/api/v1/transfers`,
        params: {
          pageSize: queryArg.pageSize,
          cursor: queryArg.cursor,
        },
      }),
    }),
    getTransfer: build.query<GetTransferApiResponse, GetTransferApiArg>({
      query: (queryArg) => ({
        url: `/api/v1/transfers/${queryArg.transferId}`,
      }),
    }),
  }),
  overrideExisting: false,
});
export { injectedRtkApi as generatedApi };
export type ListAccountsApiResponse = /** status 200 OK */ AccountListDto;
export type ListAccountsApiArg = void;
export type GetAccountApiResponse = /** status 200 OK */ AccountDto;
export type GetAccountApiArg = {
  accountId: string;
};
export type ListTransactionsApiResponse =
  /** status 200 OK */ TransactionPageDto;
export type ListTransactionsApiArg = {
  accountId: string;
  from?: string;
  to?: string;
  type?: string;
  q?: string;
  pageSize?: number | string;
  cursor?: string;
};
export type GetTransactionApiResponse = /** status 200 OK */ TransactionDto;
export type GetTransactionApiArg = {
  accountId: string;
  transactionId: string;
};
export type AskAssistantApiResponse = /** status 200 OK */ string;
export type AskAssistantApiArg = {
  assistantMessageRequest: AssistantMessageRequest;
};
export type EndAssistantConversationApiResponse = unknown;
export type EndAssistantConversationApiArg = {
  conversationId: string;
};
export type LoginApiResponse = /** status 200 OK */ LoginResponse;
export type LoginApiArg = {
  loginRequest: LoginRequest;
};
export type LogoutApiResponse = /** status 204 No Content */ Blob;
export type LogoutApiArg = void;
export type GetCurrentCustomerApiResponse = /** status 200 OK */ CustomerDto;
export type GetCurrentCustomerApiArg = void;
export type ListPayeesApiResponse = /** status 200 OK */ PayeeListDto;
export type ListPayeesApiArg = void;
export type CreateTransferQuoteApiResponse =
  /** status 201 Created */ TransferQuoteDto;
export type CreateTransferQuoteApiArg = {
  transferQuoteRequest: TransferQuoteRequest;
};
export type ExecuteTransferApiResponse = /** status 201 Created */ TransferDto;
export type ExecuteTransferApiArg = {
  /** A UUID chosen by the client. Reuse it, with the same body, to retry safely. */
  "Idempotency-Key": string;
  executeTransferRequest: ExecuteTransferRequest;
};
export type ListTransfersApiResponse = /** status 200 OK */ TransferPageDto;
export type ListTransfersApiArg = {
  pageSize?: number | string;
  cursor?: string;
};
export type GetTransferApiResponse = /** status 200 OK */ TransferDto;
export type GetTransferApiArg = {
  transferId: string;
};
export type AccountDto = {
  id: string;
  type: string;
  name: string;
  maskedNumber: string;
  currency: string;
  balance: number | string;
  availableBalance: number | string;
  asOf: string;
};
export type AccountListDto = {
  items: AccountDto[];
  totalBalance: number | string;
};
export type TransactionDto = {
  id: string;
  postedAt: string;
  description: string;
  counterparty: null | string;
  type: string;
  amount: number | string;
  signedAmount: number | string;
  runningBalance: number | string;
  category: string;
  note: null | string;
  transferId: null | string;
};
export type PageInfo = {
  pageSize: number | string;
  nextCursor: null | string;
  hasMore: boolean;
};
export type TransactionPageDto = {
  accountId: string;
  items: TransactionDto[];
  page: PageInfo;
  totalCount: number | string;
};
export type ProblemDetails = {
  type?: null | string;
  title?: null | string;
  status?: null | number | string;
  detail?: null | string;
  instance?: null | string;
};
export type AssistantMessageRequest = {
  conversationId: null | string;
  text: string;
};
export type CustomerDto = {
  id: string;
  displayName: string;
  username: string;
};
export type LoginResponse = {
  customer: CustomerDto;
  sessionToken: string;
  expiresAt: string;
};
export type LoginRequest = {
  username: string;
  password: string;
};
export type PayeeDto = {
  id: string;
  nickname: string;
  name: string;
  bankName: string;
  maskedAccountNumber: string;
  category: null | string;
};
export type PayeeListDto = {
  items: PayeeDto[];
};
export type QuoteSourceDto = {
  id: string;
  name: string;
  maskedNumber: string;
  balance: number | string;
};
export type DestinationRefDto = {
  kind: string;
  id: string;
  name: string;
  maskedNumber: string;
};
export type TransferQuoteDto = {
  quoteId: string;
  source: QuoteSourceDto;
  destination: DestinationRefDto;
  amount: number | string;
  fee: number | string;
  note: null | string;
  sourceBalanceAfter: number | string;
  expiresAt: string;
};
export type DestinationDto = {
  kind: string;
  id: null | string;
};
export type TransferQuoteRequest = {
  sourceAccountId: null | string;
  destination: null | DestinationDto;
  amount: null | number | string;
  note: null | string;
};
export type TransferSourceDto = {
  id: string;
  name: string;
  maskedNumber: string;
  balanceAfter: number | string;
};
export type TransferDestinationDto = {
  kind: string;
  id: string;
  name: string;
  maskedNumber: string;
  balanceAfter: null | number | string;
};
export type TransferDto = {
  id: string;
  reference: string;
  status: string;
  source: TransferSourceDto;
  destination: TransferDestinationDto;
  amount: number | string;
  note: null | string;
  debitTransactionId: string;
  creditTransactionId: string;
  createdAt: string;
};
export type ExecuteTransferRequest = {
  quoteId: null | string;
};
export type TransferPageDto = {
  items: TransferDto[];
  page: PageInfo;
};
