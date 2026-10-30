"use client";

import Link from "next/link";
import { appToast } from "@/lib/toast";
import { getMcpUrl } from "../AddAppGuide";

type Route = { id: string; title: string; steps: string[]; code?: string };

function routesFor(url: string): Route[] {
  return [
    {
      id: "claude-ai",
      title: "claude.ai and Claude Desktop",
      steps: [
        "Download the plugin below. It already points at this Mizan server.",
        "In Claude, open Customize, then Plugins, then Add, and choose Upload plugin. Select the zip.",
        "Open the plugin's Connectors tab, add Mizan and connect it.",
        "Sign in to Mizan when asked, and choose what Claude may do.",
      ],
    },
    {
      id: "claude-code",
      title: "Claude Code",
      steps: [
        "Download the plugin, then start Claude Code with it.",
        "Run /mcp, choose the Mizan server and sign in in the browser window that opens.",
      ],
      code: "claude --plugin-dir ./mizan-plugin.zip",
    },
    {
      id: "connector",
      title: "Only the connector",
      steps: [
        "On claude.ai or in Claude Desktop, open Customize, then Connectors, and choose Add custom connector.",
        "Paste the connector address above and add it. In Claude Code, run the command below instead.",
        "The plugin's skills are left out; Claude still sees every tool you allow.",
      ],
      code: `claude mcp add --transport http mizan ${url}`,
    },
  ];
}

export default function AddMizanToClaudePage() {
  const url = getMcpUrl();
  const download = `${url.replace(/\/$/, "")}/plugin.zip`;
  const routes = routesFor(url);

  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      appToast.success("Copied");
    } catch {
      appToast.error("Could not copy. Select the text and copy it.");
    }
  };

  return (
    <div className="mx-auto max-w-3xl space-y-6 pb-10">
      <div className="space-y-2">
        <Link
          href="/profile/mcp"
          className="text-sm text-charcoal-blue-500 hover:underline dark:text-charcoal-blue-400"
        >
          Connected apps
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
          Add Mizan to Claude
        </h1>
        <p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
          The Mizan plugin adds the connector and the skills that tell Claude
          how to log meals, plan the week and review your progress. It holds no
          key: Claude signs in to your account and you choose what it may do.
        </p>
      </div>

      <section className="card space-y-4 p-4">
        <div>
          <h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
            Connector address
          </h2>
          <div className="mt-2 flex items-center gap-2">
            <code className="min-w-0 flex-1 break-all rounded-[3px] border border-charcoal-blue-200 bg-white px-3 py-2 text-sm dark:border-white/15 dark:bg-charcoal-blue-950">
              {url}
            </code>
            <button
              type="button"
              className="btn-secondary shrink-0"
              onClick={() => void copy(url)}
            >
              Copy
            </button>
          </div>
        </div>
        <a href={download} download className="btn-primary inline-flex">
          Download plugin
        </a>
      </section>

      {routes.map((route) => (
        <section
          key={route.id}
          aria-labelledby={`route-${route.id}`}
          className="card p-4"
        >
          <h2
            id={`route-${route.id}`}
            className="mb-3 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100"
          >
            {route.title}
          </h2>
          <ol className="list-decimal space-y-2 pl-5 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-200">
            {route.steps.map((step) => (
              <li key={step}>{step}</li>
            ))}
          </ol>
          {route.code && (
            <div className="mt-4 flex items-start gap-2">
              <pre className="min-w-0 flex-1 overflow-x-auto rounded-[3px] border border-charcoal-blue-200 bg-white p-3 text-xs dark:border-white/15 dark:bg-charcoal-blue-950">
                <code>{route.code}</code>
              </pre>
              <button
                type="button"
                className="btn-secondary shrink-0"
                onClick={() => void copy(route.code!)}
              >
                Copy
              </button>
            </div>
          )}
        </section>
      ))}

      <section className="card p-4">
        <h2 className="mb-2 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
          Give it only what it needs
        </h2>
        <p className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
          When you connect, Mizan shows a permission screen. Set each area to
          No access, Read or Read and change, and choose which households
          Claude may see. Logging food needs only Read and change on food and
          nutrition. You can reduce access or disconnect
          later under{" "}
          <Link href="/profile/mcp" className="underline">
            Connected apps
          </Link>
          ; to widen it, connect again.
        </p>
      </section>
    </div>
  );
}
