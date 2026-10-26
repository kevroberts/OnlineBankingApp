import { ArrowRightLeft, ChevronRight } from "lucide-react";
import Link from "next/link";
import type { AccountDto } from "@/api/bankApi";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { formatMoney } from "@/lib/money";

const TYPE_LABELS: Record<string, string> = { checking: "Checking", savings: "Savings" };

export function AccountCard({ account }: { account: AccountDto }) {
  return (
    <Card className="transition-shadow hover:shadow-md">
      <CardHeader className="grid-cols-[1fr_auto] items-start">
        <div className="flex flex-col gap-1.5">
          <CardTitle>
            <Link href={`/accounts/${account.id}`} className="hover:underline">
              {account.name}
            </Link>
          </CardTitle>
          <span className="text-sm text-muted-foreground">{account.maskedNumber}</span>
        </div>
        <Badge variant="secondary">{TYPE_LABELS[account.type] ?? account.type}</Badge>
      </CardHeader>
      <CardContent>
        <div className="text-sm text-muted-foreground">Balance</div>
        <div className="text-3xl font-semibold tabular-nums">
          {formatMoney(account.balance, account.currency)}
        </div>
      </CardContent>
      <CardFooter className="justify-between">
        <Button asChild variant="ghost" size="sm" className="-ml-3">
          <Link
            href={`/accounts/${account.id}`}
            aria-label={`View transactions for ${account.name}`}
          >
            View transactions
            <ChevronRight aria-hidden />
          </Link>
        </Button>
        <Button asChild variant="outline" size="sm">
          <Link
            href={`/transfer?from=${account.id}`}
            aria-label={`Move money from ${account.name}`}
          >
            <ArrowRightLeft aria-hidden />
            Move money
          </Link>
        </Button>
      </CardFooter>
    </Card>
  );
}
