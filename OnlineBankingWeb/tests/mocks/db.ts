import type {
  AccountDto,
  CustomerDto,
  PayeeDto,
  TransactionDto,
  TransferDto,
  TransferQuoteDto,
} from "../../src/api/bankApi.generated";

/** A small in-memory bank for component tests and the offline demo mode (NEXT_PUBLIC_API_MOCKING=enabled). */
export interface MockDb {
  customer: CustomerDto;
  password: string;
  signedIn: boolean;
  accounts: AccountDto[];
  payees: PayeeDto[];
  transactions: Record<string, TransactionDto[]>;
  quotes: Map<string, TransferQuoteDto & { used?: string }>;
  transfers: TransferDto[];
  /** Idempotency-Key → { body, transfer } for replays. */
  executed: Map<string, { quoteId: string; transferId: string }>;
}

export const IDS = {
  customer: "a11ce000-0000-4000-8000-000000000001",
  checking: "a11ce000-0000-4000-8000-0000000000a1",
  savings: "a11ce000-0000-4000-8000-0000000000a2",
  landlord: "a11ce000-0000-4000-8000-0000000000b1",
  power: "a11ce000-0000-4000-8000-0000000000b2",
};

const NOW = "2026-10-25T14:00:00Z";

function tx(
  id: number,
  accountId: string,
  postedAt: string,
  description: string,
  signed: number,
  runningBalance: number,
  extra: Partial<TransactionDto> = {},
): TransactionDto {
  return {
    id: `${accountId.slice(0, 24)}${id.toString(16).padStart(12, "0")}`,
    postedAt,
    description,
    counterparty: null,
    type: signed < 0 ? "debit" : "credit",
    amount: Math.abs(signed),
    signedAmount: signed,
    runningBalance,
    category: "other",
    note: null,
    transferId: null,
    ...extra,
  };
}

export function createDb(): MockDb {
  return {
    customer: { id: IDS.customer, displayName: "Alice Morgan", username: "alice" },
    password: "Demo#2026",
    // In the browser demo, a page load keeps the mock session if its cookie is still there.
    signedIn: typeof document !== "undefined" && document.cookie.includes("obk_session=mock-token"),
    accounts: [
      {
        id: IDS.checking,
        type: "checking",
        name: "Everyday Checking",
        maskedNumber: "****4821",
        currency: "USD",
        balance: 2650.75,
        availableBalance: 2650.75,
        asOf: NOW,
      },
      {
        id: IDS.savings,
        type: "savings",
        name: "High-Yield Savings",
        maskedNumber: "****9137",
        currency: "USD",
        balance: 10200,
        availableBalance: 10200,
        asOf: NOW,
      },
    ],
    payees: [
      {
        id: IDS.landlord,
        nickname: "Landlord",
        name: "Oak Street Properties",
        bankName: "First Federal",
        maskedAccountNumber: "****1188",
        category: "housing",
      },
      {
        id: IDS.power,
        nickname: "City Power",
        name: "City Power & Light",
        bankName: "Commerce Bank",
        maskedAccountNumber: "****4456",
        category: "utilities",
      },
    ],
    transactions: {
      [IDS.checking]: [
        tx(5, IDS.checking, "2026-10-24T23:00:00Z", "ATM withdrawal", -413.42, 2650.75, {
          counterparty: "Main St ATM",
        }),
        tx(
          4,
          IDS.checking,
          "2026-10-24T18:39:00Z",
          "Card purchase Bean There Coffee",
          -50.1,
          3064.17,
          {
            counterparty: "Bean There Coffee",
          },
        ),
        tx(3, IDS.checking, "2026-10-20T13:00:00Z", "Payroll deposit", 2400, 3114.27, {
          counterparty: "Acme Corp",
        }),
        tx(2, IDS.checking, "2026-09-30T13:00:00Z", "Card purchase Corner Coffee", -4.5, 714.27, {
          counterparty: "Corner Coffee",
        }),
        tx(1, IDS.checking, "2026-09-02T13:00:00Z", "Opening deposit", 718.77, 718.77),
      ],
      [IDS.savings]: [tx(1, IDS.savings, "2026-09-01T13:00:00Z", "Opening deposit", 10200, 10200)],
    },
    quotes: new Map(),
    transfers: [],
    executed: new Map(),
  };
}
