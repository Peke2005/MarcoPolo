import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import fs from "fs/promises";
import path from "path";

const PROJECT_ROOT = path.resolve(process.env.FRENTE_PARTIDO_ROOT || path.join(process.cwd(), "../.."));
const SCRIPTS_DIR = path.join(PROJECT_ROOT, "Assets/Scripts");
const SO_DIR = path.join(PROJECT_ROOT, "Assets/ScriptableObjects");
const DATA_DIR = path.join(PROJECT_ROOT, "Assets/Scripts/Data");

const server = new McpServer({
  name: "frente-partido-mcp",
  version: "1.0.0",
});

// Tool: Get project structure overview
server.tool("get_project_structure", "Get overview of Frente Partido project structure", {}, async () => {
  const result = [];
  async function walkDir(dir, prefix = "") {
    try {
      const entries = await fs.readdir(dir, { withFileTypes: true });
      for (const entry of entries) {
        if (entry.name.startsWith(".")) continue;
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
          result.push(`${prefix}${entry.name}/`);
          await walkDir(fullPath, prefix + "  ");
        } else if (entry.name.endsWith(".cs") || entry.name.endsWith(".asmdef") || entry.name.endsWith(".inputactions")) {
          result.push(`${prefix}${entry.name}`);
        }
      }
    } catch { /* skip inaccessible dirs */ }
  }
  await walkDir(path.join(PROJECT_ROOT, "Assets/Scripts"));
  return { content: [{ type: "text", text: result.join("\n") || "No scripts found" }] };
});

// Tool: Read balance values from BalanceTuningData.cs
server.tool("get_balance_values", "Read current balance values from BalanceTuningData ScriptableObject definition", {}, async () => {
  const filePath = path.join(DATA_DIR, "BalanceTuningData.cs");
  try {
    const content = await fs.readFile(filePath, "utf-8");
    const values = {};
    const regex = /public\s+(?:int|float)\s+(\w+)\s*=\s*([\d.f]+)/g;
    let match;
    while ((match = regex.exec(content)) !== null) {
      values[match[1]] = match[2].replace("f", "");
    }
    return { content: [{ type: "text", text: JSON.stringify(values, null, 2) }] };
  } catch (e) {
    return { content: [{ type: "text", text: `Error reading balance: ${e.message}` }] };
  }
});

// Tool: Update a balance value
server.tool(
  "update_balance_value",
  "Update a specific balance value in BalanceTuningData.cs",
  {
    fieldName: z.string().describe("Field name (e.g., 'playerMaxHealth', 'moveSpeed')"),
    newValue: z.string().describe("New value (e.g., '120', '6.5f')"),
  },
  async ({ fieldName, newValue }) => {
    const filePath = path.join(DATA_DIR, "BalanceTuningData.cs");
    try {
      let content = await fs.readFile(filePath, "utf-8");
      const regex = new RegExp(`(public\\s+(?:int|float)\\s+${fieldName}\\s*=\\s*)[\\d.f]+`);
      if (!regex.test(content)) {
        return { content: [{ type: "text", text: `Field '${fieldName}' not found in BalanceTuningData.cs` }] };
      }
      content = content.replace(regex, `$1${newValue}`);
      await fs.writeFile(filePath, content, "utf-8");
      return { content: [{ type: "text", text: `Updated ${fieldName} = ${newValue}` }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }] };
    }
  }
);

// Tool: Get weapon stats
server.tool("get_weapon_stats", "Read weapon stats from WeaponData.cs", {}, async () => {
  const filePath = path.join(DATA_DIR, "WeaponData.cs");
  try {
    const content = await fs.readFile(filePath, "utf-8");
    const values = {};
    const regex = /public\s+(?:int|float|string)\s+(\w+)\s*=\s*([^;]+)/g;
    let match;
    while ((match = regex.exec(content)) !== null) {
      values[match[1]] = match[2].trim().replace(/[f"]/g, "");
    }
    return { content: [{ type: "text", text: JSON.stringify(values, null, 2) }] };
  } catch (e) {
    return { content: [{ type: "text", text: `Error: ${e.message}` }] };
  }
});

// Tool: Get ability definitions
server.tool("get_ability_definitions", "Read ability definition fields from AbilityDefinition.cs", {}, async () => {
  const filePath = path.join(DATA_DIR, "AbilityDefinition.cs");
  try {
    const content = await fs.readFile(filePath, "utf-8");
    return { content: [{ type: "text", text: content }] };
  } catch (e) {
    return { content: [{ type: "text", text: `Error: ${e.message}` }] };
  }
});

// Tool: List all scripts by domain
server.tool(
  "list_scripts",
  "List all C# scripts in a specific domain folder",
  {
    domain: z.string().describe("Domain folder name: Core, Networking, Player, Combat, Abilities, Match, Pickups, UI, Data"),
  },
  async ({ domain }) => {
    const domainPath = path.join(SCRIPTS_DIR, domain);
    try {
      const files = await fs.readdir(domainPath);
      const csFiles = files.filter(f => f.endsWith(".cs"));
      return { content: [{ type: "text", text: csFiles.join("\n") || "No .cs files found" }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }] };
    }
  }
);

// Tool: Read any script
server.tool(
  "read_script",
  "Read contents of a specific C# script file",
  {
    domain: z.string().describe("Domain folder (Core, Networking, Player, etc.)"),
    fileName: z.string().describe("File name with .cs extension"),
  },
  async ({ domain, fileName }) => {
    const filePath = path.join(SCRIPTS_DIR, domain, fileName);
    try {
      const content = await fs.readFile(filePath, "utf-8");
      return { content: [{ type: "text", text: content }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }] };
    }
  }
);

// Tool: Get GDD summary
server.tool("get_game_design_summary", "Get quick summary of Frente Partido game design", {}, async () => {
  const summary = `
FRENTE PARTIDO - Game Design Summary
=====================================
Genre: 2D top-down tactical shooter 1v1 online
Players: 2 (host + client via Relay)
Match: Best of 5 rounds, 90s per round
Win: Kill enemy, capture beacon, or tiebreaker

COMBAT:
- Rifle: 20 dmg, 4 rps, 8 mag, 1.4s reload
- Grenade: 40 dmg, 1 per round
- Health: 100

ABILITIES (pick 1):
- Dash: 7s CD - quick dodge
- Shield: 12s CD - frontal block 2.5s
- Mine: 14s CD - proximity trap

BEACON (Faro de Mando):
- Activates at 30s
- 5s solo capture = round win
- Contested if both inside

TIEBREAKER:
1. Higher health
2. More beacon time
3. Sudden death (15s, zone damage)

PICKUPS: Health(+25), Ammo, Armor(+30) at 20s and 55s
`;
  return { content: [{ type: "text", text: summary }] };
});

// Start server
const transport = new StdioServerTransport();
await server.connect(transport);
