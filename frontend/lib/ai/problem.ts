import { ApiError } from "@/lib/api";

/**
 * The two answers an assistant call gives instead of a result: the allowance
 * is spent (429 `ai_quota_exceeded`, with when it resets) or the provider is
 * unavailable (503 `ai_unavailable`). Both are shown where the person asked,
 * in the API's words; anything else is an ordinary error (docs/AI.md).
 */
export type AiProblem =
  | { kind: "quota"; message: string; resetsAt: string | null }
  | { kind: "unavailable"; message: string };

const QUOTA_FALLBACK = "You have used your assistant allowance for now.";
const UNAVAILABLE_FALLBACK = "The assistant is unavailable right now.";

function field(body: unknown, name: string): string | null {
  if (typeof body !== "object" || body === null) return null;
  const value = (body as Record<string, unknown>)[name];
  return typeof value === "string" && value.trim() ? value : null;
}

export function readAiProblem(error: unknown): AiProblem | null {
  if (!(error instanceof ApiError)) return null;
  const code = field(error.body, "errorCode");
  const message = field(error.body, "error");
  if (error.status === 429 && (code === null || code === "ai_quota_exceeded"))
    return {
      kind: "quota",
      message: message ?? QUOTA_FALLBACK,
      resetsAt: field(error.body, "resetsAt"),
    };
  if (error.status === 503)
    return { kind: "unavailable", message: message ?? UNAVAILABLE_FALLBACK };
  return null;
}
