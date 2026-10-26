import { describe, expect, it } from "vitest";
import { formatDate, formatDateTime, lastDays, toApiDate } from "@/lib/dates";
import { AMOUNT_PATTERN, formatMoney, formatSignedMoney } from "@/lib/money";
import { safeNext } from "@/lib/safeNext";

describe("money", () => {
  it("formats currency", () => {
    expect(formatMoney(1250)).toBe("$1,250.00");
    expect(formatMoney("10200.5")).toBe("$10,200.50");
  });

  it("shows a sign on directional amounts", () => {
    expect(formatSignedMoney(-200)).toBe("−$200.00");
    expect(formatSignedMoney(1250)).toBe("+$1,250.00");
  });

  it("accepts typed amounts with up to 2 decimals", () => {
    expect(AMOUNT_PATTERN.test("25")).toBe(true);
    expect(AMOUNT_PATTERN.test("25.5")).toBe(true);
    expect(AMOUNT_PATTERN.test("25.505")).toBe(false);
    expect(AMOUNT_PATTERN.test("-1")).toBe(false);
    expect(AMOUNT_PATTERN.test("1,000")).toBe(false);
  });
});

describe("dates in the bank's time zone (America/New_York)", () => {
  it("shows timestamps in Eastern time", () => {
    // 03:30 UTC on Oct 25 is still Oct 24 in New York.
    expect(formatDate("2026-10-25T03:30:00Z")).toBe("Oct 24, 2026");
    expect(formatDateTime("2026-10-24T23:00:00Z")).toBe("Oct 24, 2026, 7:00 PM");
  });

  it("sends filters as YYYY-MM-DD in the bank's time zone", () => {
    expect(toApiDate(new Date("2026-10-25T03:30:00Z"))).toBe("2026-10-24");
    expect(lastDays(30, new Date("2026-10-25T15:00:00Z"))).toEqual({
      from: "2026-09-26",
      to: "2026-10-25",
    });
  });
});

describe("safeNext", () => {
  it("only allows same-site paths", () => {
    expect(safeNext("/accounts/abc?type=debit")).toBe("/accounts/abc?type=debit");
    expect(safeNext("https://evil.example")).toBe("/accounts");
    expect(safeNext("//evil.example")).toBe("/accounts");
    expect(safeNext("/login")).toBe("/accounts");
    expect(safeNext(null)).toBe("/accounts");
  });
});
