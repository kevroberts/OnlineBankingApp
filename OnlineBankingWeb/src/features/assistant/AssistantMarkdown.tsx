import { Fragment, type ReactNode } from "react";

/**
 * The small Markdown subset the assistant is asked to use: paragraphs, "-" or "1." lists and
 * **bold**. Everything becomes React text nodes, so nothing the model writes is ever HTML.
 */
export function AssistantMarkdown({ text }: { text: string }) {
  const blocks: ReactNode[] = [];
  let list: { ordered: boolean; items: string[] } | null = null;
  let paragraph: string[] = [];

  const flushParagraph = () => {
    if (paragraph.length > 0) {
      blocks.push(<p key={blocks.length}>{inline(paragraph.join(" "))}</p>);
      paragraph = [];
    }
  };
  const flushList = () => {
    if (list) {
      const items = list.items.map((item, i) => <li key={i}>{inline(item)}</li>);
      blocks.push(
        list.ordered ? (
          <ol key={blocks.length} className="list-decimal space-y-0.5 pl-5">
            {items}
          </ol>
        ) : (
          <ul key={blocks.length} className="list-disc space-y-0.5 pl-5">
            {items}
          </ul>
        ),
      );
      list = null;
    }
  };

  for (const raw of text.split("\n")) {
    const line = raw.trim();
    const bullet = /^[-*•]\s+(.*)$/.exec(line);
    const numbered = /^\d+[.)]\s+(.*)$/.exec(line);
    const item = bullet ?? numbered;
    if (item) {
      flushParagraph();
      const ordered = numbered !== null && bullet === null;
      if (list && list.ordered !== ordered) flushList();
      list ??= { ordered, items: [] };
      list.items.push(item[1] ?? "");
    } else if (line === "") {
      flushParagraph();
      flushList();
    } else {
      flushList();
      paragraph.push(line.replace(/^#+\s+/, ""));
    }
  }
  flushParagraph();
  flushList();

  return <div className="space-y-2">{blocks}</div>;
}

// **bold** with no space just inside the markers, so masked numbers such as ****4821 stay text.
const BOLD = /\*\*([^*\s](?:[^*]*[^*\s])?)\*\*/g;

function inline(text: string): ReactNode {
  // split() with a capture group puts the bold parts at the odd indexes.
  return text
    .split(BOLD)
    .map((piece, i) =>
      i % 2 === 1 ? <strong key={i}>{piece}</strong> : <Fragment key={i}>{piece}</Fragment>,
    );
}

/** The answer as plain text, for the screen-reader announcement. */
export function plainText(text: string): string {
  return text.replace(BOLD, "$1");
}
