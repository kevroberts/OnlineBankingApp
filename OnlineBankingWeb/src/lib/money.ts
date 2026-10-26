/** The API sends amounts as JSON numbers; its schema also allows numeric strings. */
export type ApiAmount = number | string;

const formatters = new Map<string, Intl.NumberFormat>();

function formatter(currency: string) {
  let f = formatters.get(currency);
  if (!f) {
    f = new Intl.NumberFormat("en-US", { style: "currency", currency });
    formatters.set(currency, f);
  }
  return f;
}

export function toNumber(value: ApiAmount): number {
  return typeof value === "number" ? value : Number(value);
}

/** $1,250.00 */
export function formatMoney(value: ApiAmount, currency = "USD"): string {
  return formatter(currency).format(toNumber(value));
}

/** +$1,250.00 or −$200.00 (true minus sign), for amounts that carry a direction. */
export function formatSignedMoney(value: ApiAmount, currency = "USD"): string {
  const n = toNumber(value);
  const abs = formatter(currency).format(Math.abs(n));
  return n < 0 ? `−${abs}` : `+${abs}`;
}

/** What a customer may type: digits with up to 2 decimals. Validated as a string, sent as a number. */
export const AMOUNT_PATTERN = /^\d+(\.\d{1,2})?$/;
