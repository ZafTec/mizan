
## 2026-08-27 - AI assistant tools and markdown rendering

Both found by review of commits f322adf / ff2517e on `refactor/logging-refocus`,
fixed the same day.

### Consent axis mismatch in `record_goal_progress` (High)

`AiToolCatalogue.record_goal_progress` accepted an `actualWeight` argument but
declared `DataAxis.Nutrition`. `AiToolRunner` gates each call on the axis the
tool declares, so a user who granted the assistant nutrition writes and
explicitly denied body writes could still have a weight recorded through it -
the denial was bypassable by routing the write through the wrong tool.

Fixed by removing `actualWeight` from the tool's schema and pinning the command
field to null. Weight is only writable through `log_measurement`, which
declares `DataAxis.Body`.

Lesson: a tool touching two axes cannot be gated correctly by a single axis.
One tool, one axis.

### Markdown image exfiltration in assistant replies (High)

`AiMarkdown` rendered model output with `react-markdown`, which turns
`![](url)` into an `<img>` the browser fetches on render. A prompt injection
that persuaded the model to emit an image URL with conversation content in the
query string would exfiltrate it silently - no click, nothing visible.

Fixed by rendering images as links instead of loading them. Raw HTML was
already disabled (no `rehype-raw`), so script execution was never reachable.
