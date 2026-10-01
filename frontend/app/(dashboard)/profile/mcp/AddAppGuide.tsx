"use client";

import { useState } from "react";
import { appToast } from "@/lib/toast";

/** The address clients connect to. Production serves it from the same host as the site. */
export function getMcpUrl(): string {
	if (process.env.NEXT_PUBLIC_MCP_URL) return process.env.NEXT_PUBLIC_MCP_URL;

	if (typeof window !== "undefined") {
		const { hostname, origin } = window.location;
		if (hostname !== "localhost" && hostname !== "127.0.0.1") return `${origin}/mcp`;
	}

	return "http://localhost:5001/mcp";
}

type Client = { id: string; name: string; steps: string[]; snippet?: { label: string; code: string } };

function clientsFor(url: string): Client[] {
	return [
		{
			id: "claude",
			name: "Claude",
			steps: [
				"Open Settings, then Connectors.",
				"Choose Add custom connector and paste the address above.",
				"Sign in to Mizan when asked, and choose what Claude may do.",
			],
		},
		{
			id: "chatgpt",
			name: "ChatGPT",
			steps: [
				"Open Settings, then Connectors, and turn on developer mode if you are asked to.",
				"Add a custom connector and paste the address above.",
				"Sign in to Mizan when asked, and choose what ChatGPT may do.",
			],
		},
		{
			id: "gemini",
			name: "Gemini CLI",
			steps: [
				"Add Mizan to your Gemini CLI settings file.",
				"Run /mcp auth mizan inside Gemini CLI.",
				"Sign in to Mizan in the browser window that opens.",
			],
			snippet: {
				label: "settings.json",
				code: `{\n  "mcpServers": {\n    "mizan": { "httpUrl": "${url}" }\n  }\n}`,
			},
		},
		{
			id: "claude-code",
			name: "Claude Code",
			steps: [
				"Run the command below in your terminal.",
				"Run /mcp inside Claude Code and choose Mizan to sign in.",
				"Choose what Claude Code may do in the browser window that opens.",
			],
			snippet: { label: "Terminal", code: `claude mcp add --transport http mizan ${url}` },
		},
		{
			id: "cursor",
			name: "Cursor",
			steps: [
				"Add Mizan to .cursor/mcp.json.",
				"Open Cursor settings, then MCP, and choose Connect next to Mizan.",
				"Sign in to Mizan in the browser window that opens.",
			],
			snippet: { label: ".cursor/mcp.json", code: `{\n  "mcpServers": {\n    "mizan": { "url": "${url}" }\n  }\n}` },
		},
	];
}

export function AddAppGuide() {
	const url = getMcpUrl();
	const clients = clientsFor(url);
	const [active, setActive] = useState(clients[0].id);
	const client = clients.find((c) => c.id === active) ?? clients[0];

	const copy = async (text: string) => {
		try {
			await navigator.clipboard.writeText(text);
			appToast.success("Copied");
		} catch {
			appToast.error("Could not copy. Select the text and copy it.");
		}
	};

	return (
		<div className="space-y-5">
			<section className="card p-4">
				<h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">Mizan address</h2>
				<p className="mb-3 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Every app uses this address. There is no key to copy: each app signs in to Mizan and you choose what it may do.
				</p>
				<div className="flex items-center gap-2">
					<code className="min-w-0 flex-1 break-all rounded-[3px] border border-charcoal-blue-200 bg-white px-3 py-2 text-sm dark:border-white/15 dark:bg-charcoal-blue-950">
						{url}
					</code>
					<button type="button" className="btn-secondary shrink-0" onClick={() => void copy(url)}>
						Copy
					</button>
				</div>
			</section>

			<section className="card p-4">
				<div role="tablist" aria-label="Which app" className="mb-4 flex flex-wrap gap-1">
					{clients.map((c) => (
						<button
							key={c.id}
							role="tab"
							type="button"
							aria-selected={c.id === active}
							onClick={() => setActive(c.id)}
							className={`rounded-[3px] px-3 py-2 text-sm ${
								c.id === active
									? "bg-charcoal-blue-900 text-white dark:bg-charcoal-blue-50 dark:text-charcoal-blue-900"
									: "text-charcoal-blue-600 hover:bg-charcoal-blue-100 dark:text-charcoal-blue-300 dark:hover:bg-white/10"
							}`}
						>
							{c.name}
						</button>
					))}
				</div>

				<ol className="list-decimal space-y-2 pl-5 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-200">
					{client.steps.map((step) => (
						<li key={step}>{step}</li>
					))}
				</ol>

				{client.snippet && (
					<div className="mt-4">
						<div className="mb-1 flex items-center justify-between">
							<p className="text-xs font-semibold uppercase text-charcoal-blue-500 dark:text-charcoal-blue-400">
								{client.snippet.label}
							</p>
							<button type="button" className="btn-secondary" onClick={() => void copy(client.snippet!.code)}>
								Copy
							</button>
						</div>
						<pre className="overflow-x-auto rounded-[3px] border border-charcoal-blue-200 bg-white p-3 text-xs dark:border-white/15 dark:bg-charcoal-blue-950">
							<code>{client.snippet.code}</code>
						</pre>
					</div>
				)}
			</section>
		</div>
	);
}
