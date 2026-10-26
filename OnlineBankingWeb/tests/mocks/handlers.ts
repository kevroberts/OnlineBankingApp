import { delay, http, HttpResponse, type HttpHandler } from "msw";
import type {
  AssistantMessageRequest,
  ExecuteTransferRequest,
  LoginRequest,
  TransactionDto,
  TransactionPageDto,
  TransferDto,
  TransferQuoteDto,
  TransferQuoteRequest,
} from "../../src/api/bankApi.generated";
import { createDb, type MockDb } from "./db";

export let db: MockDb = createDb();
export function resetDb() {
  db = createDb();
}

export function problem(
  status: number,
  code: string,
  detail: string,
  extra: Record<string, unknown> = {},
) {
  return HttpResponse.json(
    {
      type: `https://onlinebanking.example/problems/${code}`,
      title: code,
      status,
      code,
      detail,
      traceId: "00-mock",
      ...extra,
    },
    { status, headers: { "Content-Type": "application/problem+json" } },
  );
}

/** A text/event-stream response that sends the given events, as the assistant endpoint does. */
export function sseResponse(events: [event: string, data: unknown][]) {
  const encoder = new TextEncoder();
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const [event, data] of events) {
        controller.enqueue(encoder.encode(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`));
      }
      controller.close();
    },
  });
  return new HttpResponse(stream, {
    headers: { "Content-Type": "text/event-stream", "Cache-Control": "no-cache" },
  });
}

const money = (n: number) => n.toLocaleString("en-US", { style: "currency", currency: "USD" });

/** A canned assistant for component tests and the offline demo: it answers balance questions only. */
function mockAssistantAnswer(text: string): string {
  if (/balance|how much (do i have|is in)/i.test(text)) {
    const named = db.accounts.find((a) => text.toLowerCase().includes(a.type));
    if (named) {
      return `Your ${named.name} (${named.maskedNumber}) balance is **${money(Number(named.balance))}**.`;
    }
    const lines = db.accounts.map((a) => `- ${a.name}: **${money(Number(a.balance))}**`);
    return `Here are your balances:\n${lines.join("\n")}`;
  }
  return "In this demo I can answer balance questions, such as **What is my checking balance?**";
}

const unauthenticated = () => problem(401, "unauthenticated", "Sign in to continue.");

const newId = () => crypto.randomUUID();

function page<T>(items: T[], pageSize: number, cursor: string | null) {
  const start = cursor ? Number(cursor) : 0;
  const slice = items.slice(start, start + pageSize);
  const next = start + pageSize < items.length ? String(start + pageSize) : null;
  return { items: slice, page: { pageSize, nextCursor: next, hasMore: next !== null } };
}

export const handlers: HttpHandler[] = [
  http.post("*/api/v1/auth/login", async ({ request }) => {
    const body = (await request.json()) as LoginRequest;
    if (body.username?.toLowerCase() !== db.customer.username || body.password !== db.password) {
      return problem(401, "invalid_credentials", "The username or password is incorrect.");
    }
    db.signedIn = true;
    return HttpResponse.json(
      {
        customer: db.customer,
        sessionToken: "mock-token",
        expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
      },
      { headers: { "Set-Cookie": "obk_session=mock-token; Path=/; SameSite=Strict" } },
    );
  }),

  http.post("*/api/v1/auth/logout", () => {
    db.signedIn = false;
    return new HttpResponse(null, {
      status: 204,
      headers: { "Set-Cookie": "obk_session=; Path=/; Max-Age=0; SameSite=Strict" },
    });
  }),

  http.get("*/api/v1/auth/me", () =>
    db.signedIn ? HttpResponse.json(db.customer) : unauthenticated(),
  ),

  http.get("*/api/v1/accounts", () => {
    if (!db.signedIn) return unauthenticated();
    const total = db.accounts.reduce((sum, a) => sum + Number(a.balance), 0);
    return HttpResponse.json({ items: db.accounts, totalBalance: Math.round(total * 100) / 100 });
  }),

  http.get("*/api/v1/accounts/:accountId", ({ params }) => {
    if (!db.signedIn) return unauthenticated();
    const account = db.accounts.find((a) => a.id === params.accountId);
    return account
      ? HttpResponse.json(account)
      : problem(404, "not_found", "The account was not found.");
  }),

  http.get("*/api/v1/accounts/:accountId/transactions", ({ params, request }) => {
    if (!db.signedIn) return unauthenticated();
    const accountId = params.accountId as string;
    if (!db.accounts.some((a) => a.id === accountId))
      return problem(404, "not_found", "The account was not found.");
    const url = new URL(request.url);
    const from = url.searchParams.get("from");
    const to = url.searchParams.get("to");
    const type = url.searchParams.get("type");
    const q = url.searchParams.get("q")?.toLowerCase();
    if (from && to && from > to)
      return problem(400, "invalid_date_range", "from must be on or before to.");
    const matches = (db.transactions[accountId] ?? []).filter(
      (t: TransactionDto) =>
        (!from || t.postedAt.slice(0, 10) >= from) &&
        (!to || t.postedAt.slice(0, 10) <= to) &&
        (!type || t.type === type) &&
        (!q || [t.description, t.counterparty, t.note].some((s) => s?.toLowerCase().includes(q))),
    );
    const pageSize = Number(url.searchParams.get("pageSize") ?? 25);
    const body: TransactionPageDto = {
      accountId,
      ...page(matches, pageSize, url.searchParams.get("cursor")),
      totalCount: matches.length,
    };
    return HttpResponse.json(body);
  }),

  http.get("*/api/v1/payees", () =>
    db.signedIn ? HttpResponse.json({ items: db.payees }) : unauthenticated(),
  ),

  http.post("*/api/v1/transfers/quotes", async ({ request }) => {
    if (!db.signedIn) return unauthenticated();
    const body = (await request.json()) as TransferQuoteRequest;
    const source = db.accounts.find((a) => a.id === body.sourceAccountId);
    if (!source) return problem(404, "not_found", "The source account was not found.");
    const destAccount =
      body.destination?.kind === "account"
        ? db.accounts.find((a) => a.id === body.destination?.id)
        : undefined;
    const destPayee =
      body.destination?.kind === "payee"
        ? db.payees.find((p) => p.id === body.destination?.id)
        : undefined;
    const amount = Number(body.amount);
    const errors = [];
    if (!(amount > 0))
      errors.push({
        field: "amount",
        code: "amount_not_positive",
        message: "Enter an amount greater than zero.",
      });
    else if (amount > Number(source.balance))
      errors.push({
        field: "amount",
        code: "insufficient_funds",
        message: "The amount is more than the available balance.",
      });
    if (!destAccount && !destPayee)
      errors.push({
        field: "destination.id",
        code: "destination_not_found",
        message: "The destination was not found.",
      });
    else if (destAccount?.id === source.id)
      errors.push({
        field: "destination.id",
        code: "same_account",
        message: "Choose a different account to transfer to.",
      });
    if (errors.length)
      return problem(422, "validation_failed", "The transfer failed one or more checks.", {
        errors,
      });

    const dest = destAccount
      ? {
          kind: "account",
          id: destAccount.id,
          name: destAccount.name,
          maskedNumber: destAccount.maskedNumber,
        }
      : {
          kind: "payee",
          id: destPayee!.id,
          name: destPayee!.nickname,
          maskedNumber: destPayee!.maskedAccountNumber,
        };
    const quote: TransferQuoteDto = {
      quoteId: newId(),
      source: {
        id: source.id,
        name: source.name,
        maskedNumber: source.maskedNumber,
        balance: source.balance,
      },
      destination: dest,
      amount,
      fee: 0,
      note: body.note,
      sourceBalanceAfter: Math.round((Number(source.balance) - amount) * 100) / 100,
      expiresAt: new Date(Date.now() + 5 * 60_000).toISOString(),
    };
    db.quotes.set(quote.quoteId, quote);
    return HttpResponse.json(quote, { status: 201 });
  }),

  http.post("*/api/v1/transfers", async ({ request }) => {
    if (!db.signedIn) return unauthenticated();
    const key = request.headers.get("Idempotency-Key");
    if (!key)
      return problem(400, "idempotency_key_required", "Send a UUID in the Idempotency-Key header.");
    const body = (await request.json()) as ExecuteTransferRequest;
    const prior = db.executed.get(key);
    if (prior) {
      if (prior.quoteId !== body.quoteId)
        return problem(422, "validation_failed", "Key reused.", {
          errors: [
            {
              field: null,
              code: "idempotency_key_reused",
              message: "Key reused with a different body.",
            },
          ],
        });
      const t = db.transfers.find((x) => x.id === prior.transferId)!;
      return HttpResponse.json(t, { status: 201, headers: { "Idempotent-Replayed": "true" } });
    }
    const quote = db.quotes.get(body.quoteId ?? "");
    if (!quote) return problem(404, "not_found", "The quote was not found.");
    if (quote.used)
      return problem(409, "quote_already_used", "This quote has already been executed.", {
        transferId: quote.used,
      });
    if (new Date(quote.expiresAt) <= new Date())
      return problem(422, "validation_failed", "Expired.", {
        errors: [{ field: "quoteId", code: "quote_expired", message: "The quote has expired." }],
      });
    await delay(50);
    const source = db.accounts.find((a) => a.id === quote.source.id)!;
    const amount = Number(quote.amount);
    source.balance = Math.round((Number(source.balance) - amount) * 100) / 100;
    source.availableBalance = source.balance;
    const destAccount = db.accounts.find((a) => a.id === quote.destination.id);
    if (destAccount) {
      destAccount.balance = Math.round((Number(destAccount.balance) + amount) * 100) / 100;
      destAccount.availableBalance = destAccount.balance;
    }
    const transfer: TransferDto = {
      id: newId(),
      reference: `TRF-20261025-${String(db.transfers.length + 1).padStart(6, "0")}`,
      status: "completed",
      source: {
        id: source.id,
        name: source.name,
        maskedNumber: source.maskedNumber,
        balanceAfter: source.balance,
      },
      destination: { ...quote.destination, balanceAfter: destAccount ? destAccount.balance : null },
      amount,
      note: quote.note,
      debitTransactionId: newId(),
      creditTransactionId: newId(),
      createdAt: new Date().toISOString(),
    };
    db.transfers.unshift(transfer);
    quote.used = transfer.id;
    db.executed.set(key, { quoteId: quote.quoteId, transferId: transfer.id });
    const debit: TransactionDto = {
      id: transfer.debitTransactionId,
      postedAt: transfer.createdAt,
      description: `Transfer to ${quote.destination.name}`,
      counterparty: quote.destination.name,
      type: "debit",
      amount,
      signedAmount: -amount,
      runningBalance: source.balance,
      category: "transfer",
      note: quote.note,
      transferId: transfer.id,
    };
    db.transactions[source.id] = [debit, ...(db.transactions[source.id] ?? [])];
    return HttpResponse.json(transfer, {
      status: 201,
      headers: { Location: `/api/v1/transfers/${transfer.id}` },
    });
  }),

  http.get("*/api/v1/transfers", ({ request }) => {
    if (!db.signedIn) return unauthenticated();
    const url = new URL(request.url);
    return HttpResponse.json(
      page(
        db.transfers,
        Number(url.searchParams.get("pageSize") ?? 25),
        url.searchParams.get("cursor"),
      ),
    );
  }),

  http.get("*/api/v1/transfers/:transferId", ({ params }) => {
    if (!db.signedIn) return unauthenticated();
    const t = db.transfers.find((x) => x.id === params.transferId);
    return t ? HttpResponse.json(t) : problem(404, "not_found", "The transfer was not found.");
  }),
  http.post("*/api/v1/assistant/messages", async ({ request }) => {
    if (!db.signedIn) return unauthenticated();
    const body = (await request.json()) as AssistantMessageRequest;
    if (!body.text?.trim()) return problem(400, "bad_request", "Ask a question.");
    const words = mockAssistantAnswer(body.text).split(/(?<= )/);
    return sseResponse([
      ["conversation", { conversationId: body.conversationId ?? newId(), mode: "mock" }],
      ["status", { tool: "list_accounts" }],
      ...words.map((delta): [string, unknown] => ["text", { delta }]),
      ["done", { stopReason: "end_turn" }],
    ]);
  }),

  http.delete("*/api/v1/assistant/conversations/:conversationId", () =>
    db.signedIn ? new HttpResponse(null, { status: 204 }) : unauthenticated(),
  ),
];
