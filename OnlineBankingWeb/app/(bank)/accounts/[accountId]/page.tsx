import type { Metadata } from "next";
import { AccountHistory } from "@/features/history/AccountHistory";

export const metadata: Metadata = { title: "Account" };

export default function AccountPage() {
  return <AccountHistory />;
}
