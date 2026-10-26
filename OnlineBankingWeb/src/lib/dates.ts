import { TZDate } from "@date-fns/tz";
import { format, subDays } from "date-fns";

/** The API's bank time zone. Date filters and displayed dates both use it. */
export const BANK_TIME_ZONE = "America/New_York";

function inBank(value: string | Date) {
  return new TZDate(typeof value === "string" ? new Date(value) : value, BANK_TIME_ZONE);
}

/** Oct 24, 2026 */
export function formatDate(value: string | Date): string {
  return format(inBank(value), "MMM d, yyyy");
}

/** Oct 24, 2026, 7:30 PM */
export function formatDateTime(value: string | Date): string {
  return format(inBank(value), "MMM d, yyyy, h:mm a");
}

/** YYYY-MM-DD in the bank's time zone, the format the API's from/to filters take. */
export function toApiDate(value: Date): string {
  return format(inBank(value), "yyyy-MM-dd");
}

export function todayInBank(now = new Date()): string {
  return toApiDate(now);
}

/** Inclusive range for "Last N days", ending today in the bank's time zone. */
export function lastDays(days: number, now = new Date()): { from: string; to: string } {
  return { from: toApiDate(subDays(inBank(now), days - 1)), to: toApiDate(now) };
}

/** Parses YYYY-MM-DD as a calendar date (no time zone shift), for display in the picker. */
export function parseApiDate(value: string): Date {
  const [y, m, d] = value.split("-").map(Number);
  return new Date(y!, (m ?? 1) - 1, d ?? 1);
}

export function formatApiDate(value: string): string {
  return format(parseApiDate(value), "MMM d, yyyy");
}

/** A date picked in the calendar is a calendar day, not an instant: format it without a time zone shift. */
export function calendarToApiDate(value: Date): string {
  return format(value, "yyyy-MM-dd");
}
