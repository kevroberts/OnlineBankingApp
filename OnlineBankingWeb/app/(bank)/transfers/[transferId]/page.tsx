import type { Metadata } from "next";
import { ResultPanel } from "@/features/transfers/ResultPanel";

export const metadata: Metadata = { title: "Transfer result" };

export default function TransferResultPage() {
  return <ResultPanel />;
}
