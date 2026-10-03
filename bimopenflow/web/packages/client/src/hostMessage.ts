/** The host's own sentence from an ApiClient error ("GET <path> -> 404: {"error":"..."}"),
 *  or the whole message when it carries none. */
export function hostMessage(e: unknown): string {
  const message = e instanceof Error ? e.message : String(e);
  const body = message.indexOf("{");
  if (body < 0) return message;
  try {
    const parsed = JSON.parse(message.slice(body)) as { error?: unknown };
    return typeof parsed.error === "string" ? parsed.error : message;
  } catch {
    return message;
  }
}
