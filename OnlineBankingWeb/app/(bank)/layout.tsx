import { Suspense, type ReactNode } from "react";
import { AppHeader } from "@/features/auth/AppHeader";
import { SessionGate } from "@/features/auth/SessionGate";

export default function BankLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col">
      <AppHeader />
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6 sm:py-8">
        <Suspense>
          <SessionGate>{children}</SessionGate>
        </Suspense>
      </main>
    </div>
  );
}
