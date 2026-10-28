import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

// The MCP Apps pages are served by the MCP service, not by Next.js; their shared bridge is plain JavaScript.
// It is loaded here as the page loads it, and only its parsers are exercised.
type Result = { content?: { type: string; text?: string }[]; structuredContent?: unknown; isError?: boolean };
const source = readFileSync(join(__dirname, "../../backend/Mizan.Mcp.Server/Apps/_bridge.js"), "utf8");
const { dataOf, textOf } = new Function(`${source}\nreturn { dataOf, textOf };`)() as {
  dataOf: (result: Result) => unknown;
  textOf: (result: Result) => string;
};

const text = (value: string) => ({ type: "text", text: value });
const hostNote = text("[This tool call rendered an interactive widget in the chat. The user can already see the result.]");

describe("the MCP Apps bridge", () => {
  it.each<[string, Result, unknown, string]>([
    ["structured content only", { structuredContent: { totalCalories: 1800 } }, { totalCalories: 1800 }, ""],
    ["structured content wins over the text", { content: [text('{"a":1}')], structuredContent: { a: 2 } }, { a: 2 }, '{"a":1}'],
    ["one JSON text block", { content: [text('{"date":"2026-10-28"}')] }, { date: "2026-10-28" }, '{"date":"2026-10-28"}'],
    ["a JSON block followed by a host note", { content: [text('{"foods":[]}'), hostNote] }, { foods: [] }, '{"foods":[]}'],
    ["a host note before the JSON block", { content: [hostNote, text("[1,2]")] }, [1, 2], hostNote.text],
    ["an error", { content: [text('{"error":"Pro only"}')], isError: true }, null, '{"error":"Pro only"}'],
    ["an error with structured content", { structuredContent: { a: 1 }, isError: true }, null, ""],
    ["text that is not JSON", { content: [text("No plan covers today.")] }, null, "No plan covers today."],
    ["a JSON scalar is not data", { content: [text("42")] }, null, "42"],
    ["no content at all", {}, null, ""],
  ])("%s", (_name, result, data, message) => {
    expect(dataOf(result)).toEqual(data);
    expect(textOf(result)).toBe(message);
  });
});
