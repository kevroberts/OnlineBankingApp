import type { Metadata } from "next";
import { PageHeader } from "@/components/PageHeader";
import { TransferList } from "@/features/transfers/TransferList";

export const metadata: Metadata = { title: "Transfers" };

export default function TransfersPage() {
  return (
    <>
      <PageHeader title="Recent transfers" />
      <TransferList />
    </>
  );
}
