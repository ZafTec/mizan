import { describe, expect, it } from "vitest";
import {
  announcementSchema,
  audienceOf,
  contactSchema,
  fieldErrors,
} from "./schema";

describe("contactSchema", () => {
  it.each([
    [
      "a complete message",
      {
        name: "Sam",
        email: "sam@example.test",
        subject: "Hi",
        message: "Hello",
      },
      {},
    ],
    [
      "no address",
      { name: "Sam", email: " ", subject: "Hi", message: "Hello" },
      { email: "Email is required" },
    ],
    [
      "an address nobody can reply to",
      { name: "Sam", email: "sam", subject: "Hi", message: "Hello" },
      { email: "Enter an email address we can reply to" },
    ],
    [
      "a message over the cap",
      {
        name: "Sam",
        email: "sam@example.test",
        subject: "Hi",
        message: "x".repeat(5001),
      },
      { message: "Keep the message under 5,000 characters" },
    ],
  ])("%s", (_, input, errors) => {
    const result = contactSchema.safeParse(input);
    expect(result.success ? {} : fieldErrors(result.error)).toEqual(errors);
  });
});

describe("announcementSchema", () => {
  it.each([
    ["everyone", { kind: "all", targetId: null }, true, "all"],
    ["pro", { kind: "pro", targetId: null }, true, "pro"],
    [
      "a household",
      { kind: "household", targetId: "h1" },
      true,
      "household:h1",
    ],
    ["a user", { kind: "user", targetId: "u1" }, true, "user:u1"],
    [
      "a household not chosen",
      { kind: "household", targetId: null },
      false,
      null,
    ],
  ] as const)("%s", (_, audience, valid, spelled) => {
    const result = announcementSchema.safeParse({
      subject: "News",
      body: "Body",
      ...audience,
    });
    expect(result.success).toBe(valid);
    if (result.success) expect(audienceOf(result.data)).toBe(spelled);
    else
      expect(fieldErrors(result.error).targetId).toBe("Choose who receives it");
  });
});
