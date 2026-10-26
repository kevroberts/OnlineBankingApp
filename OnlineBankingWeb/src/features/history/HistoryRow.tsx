import Link from "next/link";
import type { TransactionDto } from "@/api/bankApi";
import { formatDateTime } from "@/lib/dates";
import { formatMoney, formatSignedMoney } from "@/lib/money";
import { cn } from "@/lib/utils";

function TypeLabel({ type }: { type: string }) {
  return (
    <span className="text-xs text-muted-foreground">{type === "credit" ? "Credit" : "Debit"}</span>
  );
}

function Amount({ tx, currency }: { tx: TransactionDto; currency: string }) {
  return (
    <span
      className={cn(
        "font-medium tabular-nums",
        tx.type === "credit" ? "text-success" : "text-foreground",
      )}
    >
      {formatSignedMoney(tx.signedAmount, currency)}
    </span>
  );
}

function Description({ tx }: { tx: TransactionDto }) {
  const second = [tx.counterparty, tx.note].filter(Boolean).join(" · ");
  return (
    <div className="flex min-w-0 flex-col">
      {tx.transferId ? (
        <Link href={`/transfers/${tx.transferId}`} className="font-medium hover:underline">
          {tx.description}
        </Link>
      ) : (
        <span className="font-medium">{tx.description}</span>
      )}
      {second && <span className="truncate text-sm text-muted-foreground">{second}</span>}
    </div>
  );
}

/** Desktop: one table row. */
export function HistoryTableRow({ tx, currency }: { tx: TransactionDto; currency: string }) {
  return (
    <tr className="border-b last:border-0">
      <td className="py-3 pr-4 align-top text-sm whitespace-nowrap text-muted-foreground">
        {formatDateTime(tx.postedAt)}
      </td>
      <td className="py-3 pr-4 align-top">
        <Description tx={tx} />
      </td>
      <td className="py-3 pr-4 text-right align-top">
        <div className="flex flex-col items-end">
          <Amount tx={tx} currency={currency} />
          <TypeLabel type={tx.type} />
        </div>
      </td>
      <td className="py-3 text-right align-top text-sm tabular-nums">
        {formatMoney(tx.runningBalance, currency)}
      </td>
    </tr>
  );
}

/** Phone: a two-line entry with the same fields. */
export function HistoryListRow({ tx, currency }: { tx: TransactionDto; currency: string }) {
  return (
    <li className="flex flex-col gap-1 border-b py-3 last:border-0">
      <div className="flex items-start justify-between gap-3">
        <Description tx={tx} />
        <div className="flex shrink-0 flex-col items-end">
          <Amount tx={tx} currency={currency} />
          <TypeLabel type={tx.type} />
        </div>
      </div>
      <div className="flex justify-between gap-3 text-xs text-muted-foreground">
        <span>{formatDateTime(tx.postedAt)}</span>
        <span className="tabular-nums">
          Balance after {formatMoney(tx.runningBalance, currency)}
        </span>
      </div>
    </li>
  );
}
