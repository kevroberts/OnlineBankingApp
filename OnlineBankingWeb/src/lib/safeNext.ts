/** Only same-site paths are allowed as a post-login destination, never another origin. */
export function safeNext(value: string | null | undefined, fallback = "/accounts"): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.startsWith("/\\"))
    return fallback;
  if (value.startsWith("/login")) return fallback;
  return value;
}
