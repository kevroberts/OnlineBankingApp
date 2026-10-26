import { describe, expect, it } from "vitest";
import { formFieldFor, makeTransferSchema, parseDestination } from "./transferSchema";

const accounts = [
  { id: "chk", balance: 100 },
  { id: "sav", balance: "2500.50" },
];
const schema = makeTransferSchema(accounts);
const valid = { sourceAccountId: "chk", destination: "account:sav", amount: "25.50", note: "" };

function errorsFor(values: Partial<typeof valid>) {
  const result = schema.safeParse({ ...valid, ...values });
  return result.success
    ? {}
    : Object.fromEntries(result.error.issues.map((i) => [i.path.join("."), i.message]));
}

describe("transfer schema mirrors the API rules", () => {
  it("accepts a valid transfer", () => {
    expect(schema.safeParse(valid).success).toBe(true);
    expect(errorsFor({ destination: "payee:p1", amount: "100" })).toEqual({});
  });

  it.each([["0"], ["0.00"], [""], ["-5"]])(
    "rejects a zero, negative or empty amount (%s)",
    (amount) => {
      expect(errorsFor({ amount }).amount).toBeDefined();
    },
  );

  it("rejects more than 2 decimal places and non-numbers", () => {
    expect(errorsFor({ amount: "1.005" }).amount).toMatch(/2 decimal places/);
    expect(errorsFor({ amount: "abc" }).amount).toMatch(/2 decimal places/);
  });

  it("rejects more than the source balance", () => {
    expect(errorsFor({ amount: "100.01" }).amount).toMatch(/more than the available balance/);
    expect(errorsFor({ amount: "100" })).toEqual({});
    expect(
      errorsFor({ sourceAccountId: "sav", destination: "account:chk", amount: "2500.50" }),
    ).toEqual({});
  });

  it("rejects the same source and destination", () => {
    expect(errorsFor({ destination: "account:chk" }).destination).toMatch(/different account/);
  });

  it("requires a source and a destination", () => {
    expect(errorsFor({ sourceAccountId: "" }).sourceAccountId).toBeDefined();
    expect(errorsFor({ destination: "" }).destination).toBeDefined();
  });

  it("limits the note to 140 characters with no control characters", () => {
    expect(errorsFor({ note: "x".repeat(140) })).toEqual({});
    expect(errorsFor({ note: "x".repeat(141) }).note).toBeDefined();
    expect(errorsFor({ note: "line\nbreak" }).note).toMatch(/control characters/);
  });
});

describe("API field mapping", () => {
  it("maps errors[].field onto form fields", () => {
    expect(formFieldFor("amount")).toBe("amount");
    expect(formFieldFor("destination.id")).toBe("destination");
    expect(formFieldFor("sourceAccountId")).toBe("sourceAccountId");
    expect(formFieldFor("note")).toBe("note");
    expect(formFieldFor("quoteId")).toBeNull();
    expect(formFieldFor(null)).toBeNull();
  });

  it("parses destination values", () => {
    expect(parseDestination("payee:p1")).toEqual({ kind: "payee", id: "p1" });
    expect(parseDestination("account:a1")).toEqual({ kind: "account", id: "a1" });
  });
});
