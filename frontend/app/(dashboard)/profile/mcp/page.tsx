"use client";

import { useEffect, useState, type ReactNode } from "react";
import { useMcpTokens, useMcpAnalytics } from "@/lib/hooks/useMcpTokens";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import { Copy, Trash2, Plus, CheckCircle2, Activity, Clock, TrendingUp, Terminal, Monitor, Code2 } from "lucide-react";
import type { CreateMcpTokenResult } from "@/types/mcp";

function getMcpUrl(): string {
  if (process.env.NEXT_PUBLIC_MCP_URL) return process.env.NEXT_PUBLIC_MCP_URL;
  if (typeof window !== "undefined") {
    const host = window.location.hostname;
    const protocol = window.location.protocol;
    if (host !== "localhost" && host !== "127.0.0.1") {
      return `${protocol}//mcp.${host}/mcp`;
    }
  }
  return "http://localhost:5001/mcp";
}

export default function McpPage() {
  const { tokens, loading, error, fetchTokens, createToken, revokeToken } = useMcpTokens();
  const { analytics, loading: analyticsLoading, fetchAnalytics } = useMcpAnalytics();

  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [tokenName, setTokenName] = useState("");
  const [createdToken, setCreatedToken] = useState<CreateMcpTokenResult | null>(null);
  const [copiedField, setCopiedField] = useState<string | null>(null);
  const [guideTab, setGuideTab] = useState<"desktop" | "code" | "cursor">("desktop");

  useEffect(() => {
    fetchTokens();
    fetchAnalytics();
  }, [fetchTokens, fetchAnalytics]);

  const handleCreateToken = async () => {
    if (!tokenName.trim()) return;

    const result = await createToken({ name: tokenName.trim() });
    if (result) {
      setCreatedToken(result);
      setTokenName("");
      setCreateDialogOpen(false);
    }
  };

  const handleRevokeToken = async (tokenId: string) => {
    if (!confirm("Revoke this token? Connected clients will lose access.")) return;
    await revokeToken(tokenId);
  };

  const copyToClipboard = (text: string, field: string) => {
    navigator.clipboard.writeText(text);
    setCopiedField(field);
    setTimeout(() => setCopiedField(null), 2000);
  };

  const formatDate = (dateString: string) =>
    new Date(dateString).toLocaleString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });

  const mcpUrl = getMcpUrl();

  const desktopConfig = (token: string) => `{
  "mcpServers": {
    "mizan": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/server-sse",
        "${mcpUrl}"
      ],
      "env": {
        "AUTHORIZATION": "Bearer ${token}"
      }
    }
  }
}`;

  const claudeCodeCommand = (token: string) =>
    `claude mcp add mizan --transport sse "${mcpUrl}" --header "Authorization: Bearer ${token}"`;

  const cursorConfig = (token: string) => `{
  "mcpServers": {
    "mizan": {
      "url": "${mcpUrl}",
      "headers": {
        "Authorization": "Bearer ${token}"
      }
    }
  }
}`;

  return (
    <div className="max-w-5xl mx-auto space-y-6 pb-10">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm uppercase tracking-wide text-brand-600 font-semibold">Model Context Protocol</p>
          <h1 className="text-3xl font-bold text-slate-900">MCP Integration</h1>
          <p className="text-slate-500">
            Connect any MCP-compatible client to your Mizan data. Works with Claude Desktop, Claude Code, Cursor, and more.
          </p>
        </div>

        <Dialog open={createDialogOpen} onOpenChange={setCreateDialogOpen}>
          <DialogTrigger asChild>
            <button className="btn-primary inline-flex items-center gap-2">
              <Plus className="h-4 w-4" />
              Generate Token
            </button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Generate New MCP Token</DialogTitle>
              <DialogDescription>
                Create a token for any MCP-compatible client — Claude Desktop, Claude Code, Cursor, or others.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4">
              <div>
                <Label htmlFor="name">Token Name</Label>
                <Input
                  id="name"
                  placeholder="e.g., Home Laptop, Work Desktop"
                  value={tokenName}
                  onChange={(e) => setTokenName(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") handleCreateToken();
                  }}
                />
              </div>
            </div>
            <DialogFooter className="flex justify-end gap-2">
              <button className="btn-secondary" onClick={() => setCreateDialogOpen(false)}>
                Cancel
              </button>
              <button className="btn-primary" onClick={handleCreateToken} disabled={!tokenName.trim()}>
                Generate
              </button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {createdToken && (
        <Dialog open={!!createdToken} onOpenChange={() => setCreatedToken(null)}>
          <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Token Created</DialogTitle>
              <DialogDescription>Copy this value now — it won&apos;t be shown again.</DialogDescription>
            </DialogHeader>
            <div className="space-y-4">
              <div className="bg-slate-900 text-white rounded-xl p-4 flex items-center justify-between gap-3">
                <code className="text-sm break-all">{createdToken.plaintextToken}</code>
                <button
                  className="btn-secondary shrink-0 bg-white/10 border-white/20 text-white hover:bg-white/20"
                  onClick={() => copyToClipboard(createdToken.plaintextToken, "token")}
                >
                  {copiedField === "token" ? <CheckCircle2 className="h-4 w-4 text-emerald-300" /> : <Copy className="h-4 w-4" />}
                </button>
              </div>

              <div className="card p-4">
                <h4 className="font-semibold text-slate-900 mb-3">Setup Guide</h4>
                <p className="text-sm text-slate-500 mb-3">
                  Choose your MCP client and follow the instructions below.
                </p>

                <div className="flex gap-1 bg-slate-100 p-1 rounded-xl mb-4">
                  <button
                    className={`flex-1 flex items-center justify-center gap-2 px-3 py-2 rounded-lg text-sm font-medium transition-all ${
                      guideTab === "desktop"
                        ? "bg-white shadow text-slate-900"
                        : "text-slate-500 hover:text-slate-700"
                    }`}
                    onClick={() => setGuideTab("desktop")}
                  >
                    <Monitor className="h-4 w-4" />
                    <span className="hidden sm:inline">Claude Desktop</span>
                    <span className="sm:hidden">Desktop</span>
                  </button>
                  <button
                    className={`flex-1 flex items-center justify-center gap-2 px-3 py-2 rounded-lg text-sm font-medium transition-all ${
                      guideTab === "code"
                        ? "bg-white shadow text-slate-900"
                        : "text-slate-500 hover:text-slate-700"
                    }`}
                    onClick={() => setGuideTab("code")}
                  >
                    <Terminal className="h-4 w-4" />
                    <span className="hidden sm:inline">Claude Code</span>
                    <span className="sm:hidden">Code</span>
                  </button>
                  <button
                    className={`flex-1 flex items-center justify-center gap-2 px-3 py-2 rounded-lg text-sm font-medium transition-all ${
                      guideTab === "cursor"
                        ? "bg-white shadow text-slate-900"
                        : "text-slate-500 hover:text-slate-700"
                    }`}
                    onClick={() => setGuideTab("cursor")}
                  >
                    <Code2 className="h-4 w-4" />
                    Cursor
                  </button>
                </div>

                {guideTab === "desktop" && (
                  <div className="space-y-3">
                    <p className="text-sm text-slate-600">
                      Add this to your <code className="bg-slate-100 px-1.5 py-0.5 rounded text-xs">claude_desktop_config.json</code>:
                    </p>
                    <div className="relative">
                      <pre className="bg-slate-900 text-slate-50 rounded-lg p-4 text-xs overflow-x-auto">
                        {desktopConfig(createdToken.plaintextToken)}
                      </pre>
                      <button
                        className="absolute top-2 right-2 p-1.5 rounded-md bg-white/10 hover:bg-white/20 text-white"
                        onClick={() => copyToClipboard(desktopConfig(createdToken.plaintextToken), "desktop")}
                      >
                        {copiedField === "desktop" ? <CheckCircle2 className="h-3.5 w-3.5 text-emerald-300" /> : <Copy className="h-3.5 w-3.5" />}
                      </button>
                    </div>
                    <div className="text-sm text-slate-500">
                      <p>Config file locations:</p>
                      <ul className="list-disc list-inside ml-4 space-y-1 mt-1">
                        <li>macOS: <code className="bg-slate-100 px-1.5 py-0.5 rounded text-xs">~/Library/Application Support/Claude/claude_desktop_config.json</code></li>
                        <li>Windows: <code className="bg-slate-100 px-1.5 py-0.5 rounded text-xs">%APPDATA%\Claude\claude_desktop_config.json</code></li>
                      </ul>
                    </div>
                  </div>
                )}

                {guideTab === "code" && (
                  <div className="space-y-3">
                    <p className="text-sm text-slate-600">
                      Run this command in your terminal:
                    </p>
                    <div className="relative">
                      <pre className="bg-slate-900 text-slate-50 rounded-lg p-4 text-xs overflow-x-auto whitespace-pre-wrap break-all">
                        {claudeCodeCommand(createdToken.plaintextToken)}
                      </pre>
                      <button
                        className="absolute top-2 right-2 p-1.5 rounded-md bg-white/10 hover:bg-white/20 text-white"
                        onClick={() => copyToClipboard(claudeCodeCommand(createdToken.plaintextToken), "code")}
                      >
                        {copiedField === "code" ? <CheckCircle2 className="h-3.5 w-3.5 text-emerald-300" /> : <Copy className="h-3.5 w-3.5" />}
                      </button>
                    </div>
                    <p className="text-sm text-slate-500">
                      This registers the Mizan MCP server globally. Use <code className="bg-slate-100 px-1.5 py-0.5 rounded text-xs">--scope project</code> to scope it to the current directory.
                    </p>
                  </div>
                )}

                {guideTab === "cursor" && (
                  <div className="space-y-3">
                    <p className="text-sm text-slate-600">
                      Add this to your <code className="bg-slate-100 px-1.5 py-0.5 rounded text-xs">.cursor/mcp.json</code> in your project root:
                    </p>
                    <div className="relative">
                      <pre className="bg-slate-900 text-slate-50 rounded-lg p-4 text-xs overflow-x-auto">
                        {cursorConfig(createdToken.plaintextToken)}
                      </pre>
                      <button
                        className="absolute top-2 right-2 p-1.5 rounded-md bg-white/10 hover:bg-white/20 text-white"
                        onClick={() => copyToClipboard(cursorConfig(createdToken.plaintextToken), "cursor")}
                      >
                        {copiedField === "cursor" ? <CheckCircle2 className="h-3.5 w-3.5 text-emerald-300" /> : <Copy className="h-3.5 w-3.5" />}
                      </button>
                    </div>
                    <p className="text-sm text-slate-500">
                      Or add it globally via Cursor Settings &gt; MCP Servers.
                    </p>
                  </div>
                )}

                <div className="mt-4 p-3 bg-slate-50 rounded-lg border border-slate-200">
                  <p className="text-xs text-slate-500">
                    <span className="font-semibold text-slate-700">Other clients:</span>{" "}
                    Any MCP client that supports SSE transport can connect using the server URL{" "}
                    <code className="bg-slate-100 px-1.5 py-0.5 rounded">{mcpUrl}</code>{" "}
                    with the token as a Bearer authorization header.
                  </p>
                </div>
              </div>
            </div>
            <DialogFooter>
              <button className="btn-primary" onClick={() => setCreatedToken(null)}>
                Done
              </button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}

      <div className="card p-6">
        <Tabs defaultValue="tokens" className="w-full">
          <TabsList className="bg-slate-100 p-1 rounded-2xl inline-flex gap-1">
            <TabsTrigger value="tokens" className="rounded-xl px-4 py-2 data-[state=active]:bg-white data-[state=active]:shadow data-[state=active]:text-slate-900">
              Tokens
            </TabsTrigger>
            <TabsTrigger value="analytics" className="rounded-xl px-4 py-2 data-[state=active]:bg-white data-[state=active]:shadow data-[state=active]:text-slate-900">
              Analytics
            </TabsTrigger>
          </TabsList>

          <TabsContent value="tokens" className="mt-4 space-y-4">
            {loading ? (
              <div className="space-y-2">
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
              </div>
            ) : tokens.length === 0 ? (
              <div className="text-center py-10 text-slate-500">
                No tokens yet. Generate your first token to get started.
              </div>
            ) : (
              <div className="overflow-hidden rounded-2xl border border-slate-200">
                <table className="w-full text-sm">
                  <thead className="bg-slate-50 text-left">
                    <tr>
                      <th className="px-4 py-3 font-semibold text-slate-600">Name</th>
                      <th className="px-4 py-3 font-semibold text-slate-600">Status</th>
                      <th className="px-4 py-3 font-semibold text-slate-600 hidden sm:table-cell">Created</th>
                      <th className="px-4 py-3 font-semibold text-slate-600 hidden md:table-cell">Last Used</th>
                      <th className="px-4 py-3 font-semibold text-right text-slate-600">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {tokens.map((token) => (
                      <tr key={token.id} className="hover:bg-slate-50/60">
                        <td className="px-4 py-3 font-medium text-slate-900">{token.name}</td>
                        <td className="px-4 py-3">
                          <span
                            className={`inline-flex items-center gap-2 px-3 py-1.5 rounded-full text-xs font-semibold ${
                              token.isActive
                                ? "bg-emerald-50 text-emerald-700 border border-emerald-100"
                                : "bg-slate-100 text-slate-500 border border-slate-200"
                            }`}
                          >
                            <span
                              className={`h-2 w-2 rounded-full ${
                                token.isActive ? "bg-emerald-500" : "bg-slate-400"
                              }`}
                            />
                            {token.isActive ? "Active" : "Revoked"}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-slate-700 hidden sm:table-cell">{formatDate(token.createdAt)}</td>
                        <td className="px-4 py-3 text-slate-700 hidden md:table-cell">
                          {token.lastUsedAt ? formatDate(token.lastUsedAt) : "Never"}
                        </td>
                        <td className="px-4 py-3 text-right">
                          {token.isActive && (
                            <button
                              className="btn-secondary inline-flex items-center gap-2 text-destructive"
                              onClick={() => handleRevokeToken(token.id)}
                            >
                              <Trash2 className="h-4 w-4" />
                              <span className="hidden sm:inline">Revoke</span>
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </TabsContent>

          <TabsContent value="analytics" className="mt-4 space-y-4">
            {analyticsLoading ? (
              <div className="grid gap-4 md:grid-cols-3">
                <Skeleton className="h-32" />
                <Skeleton className="h-32" />
                <Skeleton className="h-32" />
              </div>
            ) : analytics ? (
              <>
                <div className="grid gap-4 md:grid-cols-3">
                  <MetricCard
                    title="Total Calls"
                    value={analytics.overview.totalCalls}
                    subtitle={`${analytics.overview.successRate.toFixed(1)}% success rate`}
                    icon={<Activity className="h-5 w-5 text-brand-600" />}
                  />
                  <MetricCard
                    title="Avg Response Time"
                    value={`${analytics.overview.averageExecutionTimeMs} ms`}
                    subtitle="Per MCP call"
                    icon={<Clock className="h-5 w-5 text-brand-600" />}
                  />
                  <MetricCard
                    title="Active Tokens"
                    value={analytics.overview.uniqueTokensUsed}
                    subtitle="Tokens in use"
                    icon={<TrendingUp className="h-5 w-5 text-brand-600" />}
                  />
                </div>

                <div className="grid gap-4 lg:grid-cols-2">
                  <div className="card p-4">
                    <div className="flex items-center justify-between mb-3">
                      <h3 className="font-semibold text-slate-900">Tool Usage</h3>
                      <span className="text-xs text-slate-500">Last 30 days</span>
                    </div>
                    <div className="divide-y divide-slate-100">
                      {analytics.toolUsage.map((tool) => (
                        <div key={tool.toolName} className="py-3 flex items-center justify-between">
                          <div>
                            <p className="font-medium text-slate-900">{tool.toolName}</p>
                            <p className="text-xs text-slate-500">
                              {tool.successCount} success / {tool.failureCount} failed
                            </p>
                          </div>
                          <div className="text-right">
                            <p className="text-sm font-semibold text-slate-900">{tool.callCount} calls</p>
                            <p className="text-xs text-slate-500">{tool.averageExecutionTimeMs} ms avg</p>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>

                  <div className="card p-4">
                    <div className="flex items-center justify-between mb-3">
                      <h3 className="font-semibold text-slate-900">Token Usage</h3>
                      <span className="text-xs text-slate-500">Most recent activity</span>
                    </div>
                    <div className="divide-y divide-slate-100">
                      {analytics.tokenUsage.map((token) => (
                        <div key={token.tokenId} className="py-3 flex items-center justify-between">
                          <div>
                            <p className="font-medium text-slate-900">{token.tokenName}</p>
                            <p className="text-xs text-slate-500">{token.callCount} calls</p>
                          </div>
                          <p className="text-xs text-slate-500">{formatDate(token.lastUsed)}</p>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              </>
            ) : (
              <div className="text-center py-10 text-slate-500">
                No analytics data yet. Start using your MCP tokens to see insights.
              </div>
            )}
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}

function MetricCard({
  title,
  value,
  subtitle,
  icon,
}: {
  title: string;
  value: string | number;
  subtitle?: string;
  icon?: ReactNode;
}) {
  return (
    <div className="card p-4 border border-slate-200">
      <div className="flex items-center justify-between mb-3">
        <div>
          <p className="text-xs uppercase text-slate-500 font-semibold">{title}</p>
          <p className="text-2xl font-bold text-slate-900">{value}</p>
          {subtitle && <p className="text-xs text-slate-500">{subtitle}</p>}
        </div>
        <div className="h-10 w-10 rounded-2xl bg-brand-50 text-brand-700 flex items-center justify-center">
          {icon}
        </div>
      </div>
    </div>
  );
}
