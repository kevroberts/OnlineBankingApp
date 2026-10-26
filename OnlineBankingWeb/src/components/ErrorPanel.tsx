"use client";

import { AlertTriangle } from "lucide-react";
import type { ApiProblem } from "@/api/problem";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { problemMessage } from "@/lib/problemMessages";

/** 500s and network failures: what happened, a retry, and the trace id for support. */
export function ErrorPanel({
  problem,
  onRetry,
  title = "Something went wrong",
}: {
  problem: ApiProblem;
  onRetry?: () => void;
  title?: string;
}) {
  return (
    <Card role="alert">
      <CardContent className="flex flex-col items-start gap-3">
        <div className="flex items-center gap-2 font-medium">
          <AlertTriangle aria-hidden className="size-5 text-destructive" />
          {title}
        </div>
        <p className="text-sm text-muted-foreground">{problemMessage(problem)}</p>
        {problem.traceId && (
          <p className="text-xs text-muted-foreground">
            Support reference: <span className="font-mono break-all">{problem.traceId}</span>
          </p>
        )}
        {onRetry && (
          <Button variant="outline" onClick={onRetry}>
            Try again
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
