# Frente Partido MCP (local)

Configuracion minima para usar el servidor MCP del proyecto Unity en desarrollo local.

## Requisitos

- Node.js 18+ instalado.
- Dependencias instaladas en `Tools/frente-partido-mcp`.

## 1) Instalar dependencias

Desde la raiz del repo:

```powershell
npm --prefix "Tools/frente-partido-mcp" install
```

## 2) Verificar arranque local

```powershell
node "Tools/frente-partido-mcp/index.js"
```

Si no hay error y proceso queda en espera, servidor MCP esta OK (stdio).

## 3) Configuracion MCP minima (cliente)

Usar `mcp.config.example.json` como base.

Ejemplo generico:

```json
{
  "mcpServers": {
    "frente-partido": {
      "command": "node",
      "args": [
        "C:/ruta/a/MarcoPolo/Tools/frente-partido-mcp/index.js"
      ]
    }
  }
}
```

Nota: servidor ya autodetecta raiz del proyecto por ubicacion del script. `FRENTE_PARTIDO_ROOT` es opcional.

## 4) Herramientas MCP expuestas

- `get_project_structure`
- `list_scripts`
- `read_script`
- `get_balance_values`
- `update_balance_value`
- `get_weapon_stats`
- `get_ability_definitions`
- `get_game_design_summary`
