import type { Metadata } from "next";
import { PageHeader } from "@/components/PageHeader";
import { TransferForm } from "@/features/transfers/TransferForm";

export const metadata: Metadata = { title: "Transfer" };

export default function TransferPage() {
  return (
    <div className="max-w-xl">
      <PageHeader title="Move money" description="Between your accounts or to a saved payee." />
      <TransferForm />
    </div>
  );
}
