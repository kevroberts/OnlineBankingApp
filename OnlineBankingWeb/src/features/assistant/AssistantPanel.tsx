"use client";

import { Loader2, MessageCircle, RotateCcw, Send } from "lucide-react";
import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";
import { useAppDispatch, useAppSelector } from "@/store/hooks";
import { AssistantMarkdown, plainText } from "./AssistantMarkdown";
import { selectAssistant, selectIsAnswering, type AssistantMessage } from "./assistantSlice";
import { askAssistant, startNewChat } from "./streamAssistant";

export const EXAMPLE_QUESTIONS = [
  "What is my checking balance?",
  "How much did I spend on groceries this month?",
  "Show my last five transactions.",
];

const ACTIVITY: Record<string, string> = {
  list_accounts: "Checking your accounts…",
  search_transactions: "Looking through your transactions…",
};

/** The "Ask" button in the header and the chat it opens. */
export function AssistantPanel() {
  const dispatch = useAppDispatch();
  const { messages, mode } = useAppSelector(selectAssistant);
  const answering = useAppSelector(selectIsAnswering);
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState("");
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const endRef = useRef<HTMLDivElement>(null);

  const last = messages.at(-1);
  useEffect(() => {
    endRef.current?.scrollIntoView?.({ block: "end" });
  }, [messages.length, last?.text]);

  const ask = (text: string) => {
    const question = text.trim();
    if (!question || answering) return;
    setDraft("");
    void dispatch(askAssistant(question));
    inputRef.current?.focus();
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    ask(draft);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault();
      ask(draft);
    }
  };

  // Screen readers hear each finished answer once, rather than every streamed word.
  const lastFinished = [...messages]
    .reverse()
    .find((m) => m.role === "assistant" && m.status !== "streaming");

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button variant="outline" size="sm">
          <MessageCircle aria-hidden />
          Ask
        </Button>
      </SheetTrigger>
      <SheetContent
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          inputRef.current?.focus();
        }}
      >
        <div className="border-b px-4 py-3 pr-12">
          <div className="flex items-center gap-2">
            <SheetTitle className="font-semibold">Ask about your accounts</SheetTitle>
            {mode === "mock" && (
              <Badge
                variant="secondary"
                title="No AI model is configured, so answers are built-in demo answers from your real data."
              >
                Demo answers
              </Badge>
            )}
          </div>
          <SheetDescription className="text-sm text-muted-foreground">
            Answers come from your own accounts. The assistant can look, but it can&apos;t move
            money or change anything.
          </SheetDescription>
        </div>

        <div className="flex-1 overflow-y-auto px-4 py-4">
          {messages.length === 0 ? (
            <div className="space-y-3">
              <p className="text-sm text-muted-foreground">Try asking:</p>
              <ul className="space-y-2">
                {EXAMPLE_QUESTIONS.map((q) => (
                  <li key={q}>
                    <Button
                      variant="outline"
                      size="sm"
                      className="h-auto w-full justify-start py-2 text-left whitespace-normal"
                      onClick={() => ask(q)}
                    >
                      {q}
                    </Button>
                  </li>
                ))}
              </ul>
            </div>
          ) : (
            <ol aria-label="Conversation" className="space-y-3">
              {messages.map((m) => (
                <MessageBubble key={m.id} message={m} />
              ))}
            </ol>
          )}
          <div ref={endRef} />
        </div>

        <div aria-live="polite" className="sr-only">
          {lastFinished && `Assistant: ${plainText(lastFinished.text)}`}
        </div>

        <form onSubmit={submit} className="space-y-2 border-t px-4 py-3">
          <label htmlFor="assistant-question" className="sr-only">
            Your question
          </label>
          <Textarea
            id="assistant-question"
            ref={inputRef}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={onKeyDown}
            rows={2}
            placeholder="Ask about balances, spending or transactions"
            className="min-h-0 resize-none"
          />
          <div className="flex items-center justify-between gap-2">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => void dispatch(startNewChat())}
              disabled={messages.length === 0 || answering}
            >
              <RotateCcw aria-hidden />
              New chat
            </Button>
            <Button type="submit" size="sm" disabled={answering || draft.trim() === ""}>
              {answering ? <Loader2 aria-hidden className="animate-spin" /> : <Send aria-hidden />}
              Send
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}

function MessageBubble({ message }: { message: AssistantMessage }) {
  const mine = message.role === "user";
  const streaming = message.status === "streaming";
  return (
    <li className={cn("flex", mine ? "justify-end" : "justify-start")}>
      <div
        aria-busy={streaming || undefined}
        className={cn(
          "max-w-[85%] rounded-lg px-3 py-2 text-sm",
          mine ? "bg-primary text-primary-foreground" : "bg-muted text-foreground",
          message.status === "error" && "border border-destructive/40 bg-destructive/10",
        )}
      >
        <span className="sr-only">{mine ? "You: " : "Assistant: "}</span>
        {mine ? (
          <p className="whitespace-pre-wrap">{message.text}</p>
        ) : (
          message.text && <AssistantMarkdown text={message.text} />
        )}
        {streaming && (!message.text || message.activity) && (
          <p
            className={cn("flex items-center gap-2 text-muted-foreground", message.text && "mt-2")}
          >
            <Loader2 aria-hidden className="size-4 animate-spin" />
            {(message.activity && ACTIVITY[message.activity]) ?? "Thinking…"}
          </p>
        )}
      </div>
    </li>
  );
}
