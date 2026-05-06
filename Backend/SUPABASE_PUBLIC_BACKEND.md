# Supabase backend publico

El backend publico activo esta en Supabase Edge Functions.

```text
https://kufkgjyeptuzptmegsmf.supabase.co/functions/v1/frentepartido-auth
```

El juego ya apunta a esa URL en `GameConfig.DEFAULT_AUTH_BASE_URL`.

## Que queda alojado en Supabase

- `users`: cuentas del juego.
- `user_stats`: partidas, victorias, derrotas y rango.
- `frentepartido-auth`: API publica para login, registro, verificacion y stats.

## Endpoints

```text
GET  /health
POST /auth/register
POST /auth/login
GET  /auth/verify
GET  /profile/stats
POST /profile/match
```

## Seguridad

No se sube `Backend/.env` ni password de base de datos a GitHub.
El build no necesita Radmin para login/stats porque llama a la Edge Function publica.

## Fallback local

El backend Docker local sigue existiendo para desarrollo. Solo hace falta si Supabase cae o quieres probar offline.
