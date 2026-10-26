import type { Metadata, Viewport } from "next";
import { NuqsAdapter } from "nuqs/adapters/next/app";
import type { ReactNode } from "react";
import { ApiMocking } from "@/components/ApiMocking";
import { Toaster } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";
import { StoreProvider } from "@/store/StoreProvider";
import "./globals.css";

export const metadata: Metadata = {
  title: { default: "Online Banking", template: "%s · Online Banking" },
  description: "Check your balances, review transactions and move money.",
};

export const viewport: Viewport = { width: "device-width", initialScale: 1 };

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body className="min-h-dvh font-sans">
        <ApiMocking>
          <StoreProvider>
            <NuqsAdapter>
              <TooltipProvider delayDuration={200}>
                {children}
                <Toaster />
              </TooltipProvider>
            </NuqsAdapter>
          </StoreProvider>
        </ApiMocking>
      </body>
    </html>
  );
}
