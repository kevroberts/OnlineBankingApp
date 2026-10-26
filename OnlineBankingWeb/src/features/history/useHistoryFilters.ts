"use client";

import { createParser, parseAsString, parseAsStringLiteral, useQueryStates } from "nuqs";

const apiDate = createParser({
  parse: (value) => (/^\d{4}-\d{2}-\d{2}$/.test(value) ? value : null),
  serialize: (value: string) => value,
});

export const TRANSACTION_TYPES = ["debit", "credit"] as const;
export type TransactionType = (typeof TRANSACTION_TYPES)[number];

const filterParsers = {
  from: apiDate,
  to: apiDate,
  type: parseAsStringLiteral(TRANSACTION_TYPES),
  q: parseAsString,
};

/** History filters live in the URL (?from=…&to=…&type=…&q=…), so refresh, Back and shared links keep them. */
export function useHistoryFilters() {
  const [filters, setFilters] = useQueryStates(filterParsers, { history: "replace" });
  const active = Boolean(filters.from || filters.to || filters.type || filters.q);
  const clear = () => setFilters({ from: null, to: null, type: null, q: null });
  return { filters, setFilters, active, clear };
}

export type HistoryFilterValues = ReturnType<typeof useHistoryFilters>["filters"];
