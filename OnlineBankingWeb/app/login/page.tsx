import type { Metadata } from "next";
import { Suspense } from "react";
import { Landmark } from "lucide-react";
import { LoginForm } from "@/features/auth/LoginForm";

export const metadata: Metadata = { title: "Sign in" };

export default function LoginPage() {
  return (
    <main className="mx-auto flex min-h-dvh w-full max-w-sm flex-col justify-center gap-6 px-4 py-10">
      <div className="flex flex-col items-center gap-2 text-center">
        <span className="flex size-11 items-center justify-center rounded-full bg-primary text-primary-foreground">
          <Landmark aria-hidden className="size-5" />
        </span>
        <h1 className="text-2xl font-semibold">Sign in to Online Banking</h1>
        <p className="text-sm text-muted-foreground">
          Demo users: alice, ben or chloe, password Demo#2026.
        </p>
      </div>
      <Suspense>
        <LoginForm />
      </Suspense>
    </main>
  );
}
