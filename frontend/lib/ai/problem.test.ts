import { describe, expect, it } from "vitest";
import { ApiError } from "@/lib/api";
import { readAiProblem } from "./problem";

const failure = (status: number, body: unknown) =>
  new ApiError(status, "", body);

describe("readAiProblem", () => {
  it.each([
    [
      "a spent allowance with its reset",
      failure(429, {
        errorCode: "ai_quota_exceeded",
        error:
          "You have used your AI allowance for now. It resets at 00:00 UTC.",
        scope: "user",
        resetsAt: "2026-10-26T00:00:00Z",
      }),
      {
        kind: "quota",
        message:
          "You have used your AI allowance for now. It resets at 00:00 UTC.",
        resetsAt: "2026-10-26T00:00:00Z",
      },
    ],
    [
      "a spent allowance without a body",
      failure(429, {}),
      {
        kind: "quota",
        message: "You have used your assistant allowance for now.",
        resetsAt: null,
      },
    ],
    [
      "an unavailable provider",
      failure(503, {
        errorCode: "ai_unavailable",
        error: "The assistant is not configured on this server.",
      }),
      {
        kind: "unavailable",
        message: "The assistant is not configured on this server.",
      },
    ],
    [
      "a 503 with plain text",
      failure(503, { raw: "Service Unavailable" }),
      {
        kind: "unavailable",
        message: "The assistant is unavailable right now.",
      },
    ],
    [
      "a locked account is not a quota",
      failure(429, { errorCode: "account_locked" }),
      null,
    ],
    [
      "a validation error",
      failure(400, { errorCode: "validation_failed" }),
      null,
    ],
    ["a network error", new Error("Failed to fetch"), null],
  ])("%s", (_, error, expected) => {
    expect(readAiProblem(error)).toEqual(expected);
  });
});
