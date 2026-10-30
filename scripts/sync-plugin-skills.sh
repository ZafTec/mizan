#!/usr/bin/env bash
# The plugin ships the same skills, manifest, README and licence the MCP server serves and zips
# at /mcp/plugin.zip. Edit them under backend/Mizan.Mcp.Server (Skills/ and Plugin/), then run
# this to copy them into plugin/. plugin/.mcp.json is the repository's own connector and is kept.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
server="$root/backend/Mizan.Mcp.Server"
rm -rf "$root/plugin/skills"
cp -r "$server/Skills" "$root/plugin/skills"
cp "$server/Plugin/plugin.json" "$root/plugin/.claude-plugin/plugin.json"
cp "$server/Plugin/README.md" "$root/plugin/README.md"
cp "$server/Plugin/LICENSE" "$root/plugin/LICENSE"
echo "Copied skills, plugin.json, README.md and LICENSE into plugin/"
