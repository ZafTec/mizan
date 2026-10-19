#!/usr/bin/env bash
# The plugin ships the same skills the MCP server serves. Edit them under
# backend/Mizan.Mcp.Server/Skills, then run this to copy them into plugin/skills.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
rm -rf "$root/plugin/skills"
cp -r "$root/backend/Mizan.Mcp.Server/Skills" "$root/plugin/skills"
echo "Copied skills into plugin/skills"
