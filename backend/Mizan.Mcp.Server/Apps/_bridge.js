// Talks to the host over postMessage (MCP Apps). No libraries, so the page is one file.
// Shared by every app page: MizanApps.Resources() inlines it at each page's bridge marker when the
// page is served, so the page the host receives is still one self-contained file.
const pending = new Map();
let nextId = 0;
const send = (message) => parent.postMessage(message, "*");
const request = (method, params) => new Promise((resolve, reject) => {
  const id = ++nextId;
  pending.set(id, { resolve, reject });
  send({ jsonrpc: "2.0", id, method, params });
});
const notify = (method, params) => send({ jsonrpc: "2.0", method, params: params || {} });
const callTool = (name, args) => request("tools/call", { name, arguments: args || {} });
const textOf = (result) => ((result && result.content) || []).filter((c) => c.type === "text").map((c) => c.text).join("\n");
const jsonOf = (result) => { try { return JSON.parse(textOf(result)); } catch { return null; } };
const el = (tag, props, ...kids) => {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(props || {})) {
    if (key === "class") node.className = value;
    else if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
    else if (value !== undefined && value !== null && value !== false) node.setAttribute(key, value === true ? "" : value);
  }
  for (const kid of kids.flat()) if (kid !== null && kid !== undefined && kid !== false) node.append(kid instanceof Node ? kid : document.createTextNode(String(kid)));
  return node;
};
const num = (n) => Math.round(Number(n) || 0).toLocaleString();
const reportSize = () => notify("ui/notifications/size-changed", { width: document.documentElement.scrollWidth, height: document.documentElement.scrollHeight });
const applyContext = (context) => { if (context && context.theme) document.documentElement.dataset.theme = context.theme; };
window.addEventListener("message", (event) => {
  const message = event.data;
  if (!message || message.jsonrpc !== "2.0") return;
  if (message.id !== undefined && !message.method) {
    const waiting = pending.get(message.id);
    if (!waiting) return;
    pending.delete(message.id);
    if (message.error) waiting.reject(new Error(message.error.message || "The request failed")); else waiting.resolve(message.result);
  } else if (message.method === "ui/notifications/tool-result") window.onToolResult(message.params);
  else if (message.method === "ui/notifications/host-context-changed") applyContext(message.params);
});
const connect = (name) => request("ui/initialize", { protocolVersion: "2025-06-18", appInfo: { name, version: "1.0.0" }, appCapabilities: {} })
  .then((result) => { applyContext(result && result.hostContext); notify("ui/notifications/initialized"); })
  .catch(() => {})
  .finally(() => { new ResizeObserver(reportSize).observe(document.body); reportSize(); });
