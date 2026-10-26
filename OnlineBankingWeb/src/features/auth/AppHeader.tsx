"use client";

import { Landmark, LogOut } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useLogoutMutation } from "@/api/bankApi";
import { sessionEnded } from "@/api/baseApi";
import { Button } from "@/components/ui/button";
import { AssistantPanel } from "@/features/assistant/AssistantPanel";
import { cn } from "@/lib/utils";
import { useAppDispatch, useAppSelector } from "@/store/hooks";
import { selectCustomer } from "./sessionSlice";

const NAV = [
  { href: "/accounts", label: "Accounts" },
  { href: "/transfer", label: "Transfer" },
  { href: "/transfers", label: "Transfers" },
];

export function AppHeader() {
  const pathname = usePathname();
  const router = useRouter();
  const dispatch = useAppDispatch();
  const customer = useAppSelector(selectCustomer);
  const [logout, { isLoading }] = useLogoutMutation();

  const signOut = async () => {
    await logout();
    // The API has already cleared the cookie; the listener only drops cached data and the draft.
    dispatch(sessionEnded({ serverSessionCleared: true }));
    router.replace("/login");
  };

  return (
    <header className="border-b bg-card">
      <div className="mx-auto flex max-w-5xl flex-wrap items-center gap-x-6 gap-y-2 px-4 py-3">
        <Link href="/accounts" className="flex items-center gap-2 font-semibold">
          <Landmark aria-hidden className="size-5 text-primary" />
          Online Banking
        </Link>
        <nav aria-label="Main" className="order-3 flex w-full gap-1 sm:order-none sm:w-auto">
          {NAV.map((item) => {
            const active =
              item.href === "/transfer"
                ? pathname.startsWith("/transfer/") || pathname === "/transfer"
                : pathname.startsWith(item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "rounded-md px-3 py-1.5 text-sm font-medium text-muted-foreground hover:bg-accent hover:text-accent-foreground",
                  active && "bg-secondary text-secondary-foreground",
                )}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="ml-auto flex items-center gap-3">
          {customer && (
            <span className="hidden text-sm text-muted-foreground sm:inline">
              {customer.displayName}
            </span>
          )}
          <AssistantPanel />
          <Button variant="outline" size="sm" onClick={signOut} disabled={isLoading}>
            <LogOut aria-hidden />
            Sign out
          </Button>
        </div>
      </div>
    </header>
  );
}
