import type { Metadata } from "next";
import { ConfirmPanel } from "@/features/transfers/ConfirmPanel";

export const metadata: Metadata = { title: "Confirm transfer" };

export default function ConfirmPage() {
  return <ConfirmPanel />;
}
