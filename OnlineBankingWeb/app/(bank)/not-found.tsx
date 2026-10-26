import Link from "next/link";
import { Button } from "@/components/ui/button";

export default function BankNotFound() {
  return (
    <div className="flex flex-col items-start gap-4">
      <h1 className="text-2xl font-semibold">Not found</h1>
      <p className="text-muted-foreground">
        We couldn&apos;t find that. It may belong to another customer or no longer exist.
      </p>
      <Button asChild>
        <Link href="/accounts">Back to your accounts</Link>
      </Button>
    </div>
  );
}
