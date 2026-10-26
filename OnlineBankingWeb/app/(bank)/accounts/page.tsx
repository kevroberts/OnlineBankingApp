import type { Metadata } from "next";
import { AccountsSummary } from "@/features/accounts/AccountsSummary";

export const metadata: Metadata = { title: "Accounts" };

export default function AccountsPage() {
  return <AccountsSummary />;
}
