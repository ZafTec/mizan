# Mizan

Log meals, workouts and body measurements, plan your week, build shopping lists and review your progress, all from a conversation with Claude.

## What it adds

The plugin is two things in one package:

- The **Mizan connector**, a remote MCP server at `https://mizan.zaftech.co/mcp`. It lets Claude read and write your Mizan data. You sign in to your own Mizan account and choose what Claude may do.
- Six **skills** that tell Claude how to use the connector well:
  - `mizan-food-logging` logs meals with the right serving counts
  - `mizan-recipes` turns logged meals into reusable recipes and manages them
  - `mizan-meal-planning` plans meals and builds shopping lists
  - `mizan-training` logs and reviews workouts
  - `mizan-progress-review` runs check-ins against your goal
  - `mizan-coaching` supports a trainer and client relationship

The folder holds `.claude-plugin/plugin.json`, `.mcp.json`, `skills/`, this README and a license. It holds no credential.

## Get the plugin

There are two copies of the same plugin:

- **The download from a Mizan server.** Every Mizan deployment serves a ready-made zip at `https://<host>/mcp/plugin.zip`. Its `.mcp.json` points at that deployment's own `/mcp`, so the zip from the demo at `https://mizan-demo.zaftech.co/mcp/plugin.zip` connects to the demo. In Mizan, **More → Connected apps → Add Mizan to Claude** (`/profile/mcp/claude`) has the steps, a **Download plugin** button and the connector URL to copy. This is the easiest route for claude.ai and Claude Desktop.
- **The `plugin/` folder in this repository.** It points at `https://mizan.zaftech.co/mcp`. Use it in Claude Code and when you change the plugin.

## Add it on claude.ai or in Claude Desktop

1. Download the zip from your Mizan server, as above. To use the repository folder instead, zip the `plugin` folder. The archive may hold the folder itself or its contents, as long as it holds one `.claude-plugin/plugin.json`.
2. Go to **Customize → Plugins → Add → Upload plugin** and select the zip. The same page exists in claude.ai and in the desktop app.
3. Open the plugin and select its **Connectors** tab. Mizan shows **Not added** or **Not connected**. Add it, then connect it. Mizan opens its permission screen in your browser ([below](#choose-what-claude-may-do)).

The skills load whether or not the connector is connected, but they cannot reach your data until it shows **Connected**. A plugin added to your account also reaches Cowork, and Claude Code as a synced plugin (`mizan@synced`) the next time you start it signed in to the same account.

On Team and Enterprise plans, an Owner may have to add the connector for the organization before members can connect it.

## Install it in Claude Code

To try it for one session, start Claude Code with the folder or the downloaded zip:

```bash
claude --plugin-dir ./plugin
claude --plugin-dir ./plugin.zip
```

A `--plugin-dir` copy loads in place for that session only, and replaces an installed plugin with the same name.

To install it for every session, use a marketplace (see [Publish through a marketplace](#publish-through-a-marketplace)), or add it on claude.ai so that it syncs.

Then sign in: run `/mcp`, select the Mizan server (`plugin:mizan:mizan`) and complete the browser login. From a shell, `claude mcp login` with the server name does the same. Claude Code stores and refreshes the token. **Clear authentication** in `/mcp` signs out.

In the desktop app's **Code** tab, use **+ → Add plugins** (or **Plugins → Browse plugins**) for plugins from a marketplace you added.

## Use it

Ask in plain words: "log two eggs and toast for breakfast", "plan my dinners for the week and make the shopping list", "how is my weight trending this month".

Some tools show a small view in the chat, such as a day's nutrition or a photo of a meal to confirm before it is logged.

## Choose what Claude may do

When you connect, Mizan asks you to sign in and shows a permission screen (`/oauth/consent`). Grant only what you need:

- **Areas**: profile, nutrition, training, body, recipes, planning, households, social, notifications, trainer, and AI use. Each is none, read, or write. Write includes read. Searching foods and logging them needs only nutrition write.
- **Households**: none, selected ones, or all. Meal plans and shopping lists that belong to a household are visible only for the households you allow.
- **Admin** is offered to administrators only and is off by default.

Claude sees only the tools your choice covers. When it asks for something outside it, it says which permission is missing.

To change access later, go to **More → Connected apps** (`/profile/mcp`). It shows last use and 30-day usage per app and tool. **Edit access** can only reduce access; to widen it, disconnect and connect again so that the permission screen appears. **Disconnect** revokes the connection. Both take effect on the app's next request.

## Use another Mizan server

The repository folder points at `https://mizan.zaftech.co/mcp`. Do not change that default in the repository. To use the demo or a self-hosted server, pick one:

- **Download the plugin from that server.** `https://<host>/mcp/plugin.zip` already points at it. This works on claude.ai, in Claude Desktop and in Claude Code.
- **Add the server as a connector by URL**, without the plugin's connector. On claude.ai or in Claude Desktop, go to **Customize → Connectors → Add custom connector**, enter `https://<host>/mcp` and click **Add**. The Free plan allows one custom connector; on Team and Enterprise an Owner adds it under **Organization settings → Connectors**. In Claude Code, run `claude mcp add --transport http mizan-demo https://mizan-demo.zaftech.co/mcp`. The skills work with either connector, because they name tools, not a server.
- **Edit a local copy.** Copy the `plugin` folder outside the repository, change `url` in its `.mcp.json`, and load or zip that copy.

Each server has its own accounts. Sign in with an account on the server you connect to.

## Data

Claude sends what you ask it to log or look up to your Mizan account on the server the connector names (mizan.zaftech.co by default), and receives your Mizan data back. The plugin stores nothing itself and holds no credentials: access is a revocable OAuth connection you manage under **More → Connected apps** in Mizan, and you can disconnect it at any time. The free plan allows 15 connector calls per month.

## Update and validate

Raise `version` in `.claude-plugin/plugin.json` on every release. Claude Code compares that string to decide whether there is an update: if it does not change, `claude plugin update` reports that the plugin is already at the latest version and keeps the old copy.

Validate before you share a change:

```bash
claude plugin validate --strict ./plugin
```

It prints `Validation passed` when the manifest, the skills and the MCP entry load.

How each copy picks up a new version:

- **`--plugin-dir`**: loads the folder as it is at session start, or after `/reload-plugins`.
- **Installed from a marketplace**: `claude plugin update mizan@<marketplace>`, or **Update now** on the **Installed** tab of `/plugin`. Auto-update is off by default for marketplaces other than Anthropic's; turn it on from the **Marketplaces** tab of `/plugin`. Run `/reload-plugins` or start a new session to load the update.
- **Uploaded on claude.ai**: upload the new zip. The published docs do not describe replacing an uploaded plugin; removing the old one from its menu (**Remove**) and uploading again is the path we expect, and it was not checked against published docs.
- **Added from a marketplace on claude.ai**: **Check for updates** on the marketplace, or **Sync automatically** for a github.com marketplace.

## Publish through a marketplace

A marketplace lets people install the plugin by name and update it with a command. This repository has no marketplace file. To add one, create `.claude-plugin/marketplace.json` at the repository root:

```json
{
  "name": "zaftech",
  "owner": { "name": "ZafTech" },
  "plugins": [
    { "name": "mizan", "source": "./plugin", "description": "Log meals, workouts and body measurements with your Mizan account." }
  ]
}
```

The entry `name` must match `name` in `plugin.json`, and `source` is relative to the repository root. Check it with `claude plugin validate .` from the repository root. Then:

```bash
claude plugin marketplace add ZafTec/mizan
claude plugin install mizan@zaftech
```

In a session, `/plugin marketplace add ZafTec/mizan` and `/plugin install mizan@zaftech` do the same. On claude.ai, **Customize → Plugins → Add → Add marketplace** takes the repository URL; a private repository needs the Claude GitHub App. A marketplace copy points at `https://mizan.zaftech.co/mcp`.

To list the plugin in Anthropic's directory, submit it from `claude.ai/directory/manage` (see [MCP](../docs/MCP.md#claude-plugin)).

## Keeping the skills in step

The skills in `skills/` are the same files the Mizan MCP server serves over the Skills extension. They are copied from `backend/Mizan.Mcp.Server/Skills`. A test fails if the two differ, and `scripts/sync-plugin-skills.sh` copies them again. Edit the server's copy, never `skills/` directly.

## Sources

The Claude steps above follow these pages as of October 2026: [Plugins on claude.ai](https://claude.com/docs/plugins/overview), [Plugin structure and testing](https://claude.com/docs/plugins/build.md), [Plugin support by app](https://claude.com/docs/plugins/platform-support), [Add a connector that isn't in the directory](https://claude.com/docs/connectors/custom/add-unlisted), [Install plugins in Claude Code](https://code.claude.com/docs/en/plugins/install.md), [Publish a plugin](https://code.claude.com/docs/en/plugins/publish.md), [Create a marketplace](https://code.claude.com/docs/en/plugin-marketplaces), [Plugin loading](https://code.claude.com/docs/en/plugins/loading.md) and [MCP in Claude Code](https://code.claude.com/docs/en/mcp).
