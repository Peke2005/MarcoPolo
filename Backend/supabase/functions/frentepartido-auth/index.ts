import { createClient } from "npm:@supabase/supabase-js@2";

type UserRow = {
  id: number;
  username: string;
  display_name: string;
  password_hash?: string;
};

type StatsRow = {
  user_id: number;
  matches_played: number;
  wins: number;
  losses: number;
};

const FUNCTION_SLUG = "frentepartido-auth";
const TOKEN_TTL_SECONDS = 60 * 60 * 24 * 7;
const PBKDF2_ITERATIONS = 100000;
const encoder = new TextEncoder();
const decoder = new TextDecoder();

const supabaseUrl = Deno.env.get("SUPABASE_URL") ?? "";
const serviceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "";
const jwtSecret = Deno.env.get("JWT_SECRET") || serviceKey;

if (!supabaseUrl || !serviceKey) {
  console.error("Missing SUPABASE_URL or SUPABASE_SERVICE_ROLE_KEY");
}

const db = createClient(supabaseUrl, serviceKey, {
  auth: { persistSession: false, autoRefreshToken: false },
});

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
};

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      ...corsHeaders,
      "Content-Type": "application/json",
      "Connection": "keep-alive",
    },
  });
}

function base64UrlEncode(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replaceAll("+", "-").replaceAll("/", "_").replaceAll("=", "");
}

function base64UrlEncodeString(value: string): string {
  return base64UrlEncode(encoder.encode(value));
}

function base64UrlDecode(value: string): Uint8Array {
  const padded = value.replaceAll("-", "+").replaceAll("_", "/") + "=".repeat((4 - value.length % 4) % 4);
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

async function hmacSha256(message: string): Promise<string> {
  const key = await crypto.subtle.importKey(
    "raw",
    encoder.encode(jwtSecret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const signature = await crypto.subtle.sign("HMAC", key, encoder.encode(message));
  return base64UrlEncode(new Uint8Array(signature));
}

async function signToken(user: UserRow): Promise<string> {
  const now = Math.floor(Date.now() / 1000);
  const header = base64UrlEncodeString(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const payload = base64UrlEncodeString(JSON.stringify({
    userId: user.id,
    username: user.username,
    iat: now,
    exp: now + TOKEN_TTL_SECONDS,
  }));
  const message = `${header}.${payload}`;
  const signature = await hmacSha256(message);
  return `${message}.${signature}`;
}

async function verifyToken(token: string): Promise<{ userId: number; username: string }> {
  const parts = token.split(".");
  if (parts.length !== 3) throw new Error("bad token");

  const message = `${parts[0]}.${parts[1]}`;
  const expected = await hmacSha256(message);
  if (expected !== parts[2]) throw new Error("bad signature");

  const payload = JSON.parse(decoder.decode(base64UrlDecode(parts[1])));
  if (!payload.userId || !payload.exp || payload.exp < Math.floor(Date.now() / 1000)) {
    throw new Error("expired token");
  }

  return { userId: Number(payload.userId), username: String(payload.username ?? "") };
}

async function hashPassword(password: string): Promise<string> {
  const salt = crypto.getRandomValues(new Uint8Array(16));
  const key = await crypto.subtle.importKey("raw", encoder.encode(password), "PBKDF2", false, ["deriveBits"]);
  const bits = await crypto.subtle.deriveBits(
    { name: "PBKDF2", salt, iterations: PBKDF2_ITERATIONS, hash: "SHA-256" },
    key,
    256,
  );
  return `pbkdf2$${PBKDF2_ITERATIONS}$${base64UrlEncode(salt)}$${base64UrlEncode(new Uint8Array(bits))}`;
}

async function verifyPassword(password: string, stored: string): Promise<boolean> {
  const [scheme, iterationsRaw, saltRaw, hashRaw] = stored.split("$");
  if (scheme !== "pbkdf2" || !iterationsRaw || !saltRaw || !hashRaw) return false;

  const key = await crypto.subtle.importKey("raw", encoder.encode(password), "PBKDF2", false, ["deriveBits"]);
  const bits = await crypto.subtle.deriveBits(
    { name: "PBKDF2", salt: base64UrlDecode(saltRaw), iterations: Number(iterationsRaw), hash: "SHA-256" },
    key,
    256,
  );
  return base64UrlEncode(new Uint8Array(bits)) === hashRaw;
}

async function readJson(req: Request): Promise<Record<string, unknown>> {
  try {
    const parsed = await req.json();
    return parsed && typeof parsed === "object" ? parsed as Record<string, unknown> : {};
  } catch {
    return {};
  }
}

function cleanText(value: unknown, maxLength: number): string {
  return String(value ?? "").trim().slice(0, maxLength);
}

function rankFor(matches: number, wins: number, winRate: number): string {
  if (matches <= 0) return "SIN RANGO";
  if (wins >= 30 && winRate >= 70) return "ELITE";
  if (wins >= 18 && winRate >= 60) return "ORO";
  if (wins >= 10 && winRate >= 50) return "PLATA";
  if (wins >= 4) return "BRONCE";
  return "RECLUTA";
}

function formatStats(row: Partial<StatsRow> | null) {
  const matchesPlayed = Number(row?.matches_played ?? 0);
  const wins = Number(row?.wins ?? 0);
  const losses = Number(row?.losses ?? 0);
  const winRate = matchesPlayed > 0 ? (wins * 100) / matchesPlayed : 0;
  return { matchesPlayed, wins, losses, winRate, rank: rankFor(matchesPlayed, wins, winRate) };
}

async function findUserById(userId: number): Promise<UserRow | null> {
  const { data, error } = await db
    .from("users")
    .select("id, username, display_name")
    .eq("id", userId)
    .maybeSingle();
  if (error) throw error;
  return data as UserRow | null;
}

async function requireAuth(req: Request): Promise<UserRow> {
  const header = req.headers.get("authorization") ?? "";
  if (!header.startsWith("Bearer ")) throw new Response(JSON.stringify({ error: "Token no proporcionado" }), { status: 401 });

  let decoded: { userId: number; username: string };
  try {
    decoded = await verifyToken(header.slice("Bearer ".length));
  } catch {
    throw new Response(JSON.stringify({ error: "Token invalido o expirado" }), { status: 401 });
  }

  const user = await findUserById(decoded.userId);
  if (!user) throw new Response(JSON.stringify({ error: "Usuario no encontrado" }), { status: 401 });
  return user;
}

async function getStats(userId: number) {
  const inserted = await db
    .from("user_stats")
    .insert({ user_id: userId })
    .select("user_id, matches_played, wins, losses")
    .maybeSingle();

  if (inserted.data) return formatStats(inserted.data as StatsRow);

  const { data, error } = await db
    .from("user_stats")
    .select("user_id, matches_played, wins, losses")
    .eq("user_id", userId)
    .maybeSingle();
  if (error) throw error;
  return formatStats(data as StatsRow | null);
}

async function recordMatch(userId: number, won: boolean) {
  await getStats(userId);

  const { data: current, error: readError } = await db
    .from("user_stats")
    .select("matches_played, wins, losses")
    .eq("user_id", userId)
    .single();
  if (readError) throw readError;

  const next = {
    matches_played: Number(current.matches_played ?? 0) + 1,
    wins: Number(current.wins ?? 0) + (won ? 1 : 0),
    losses: Number(current.losses ?? 0) + (won ? 0 : 1),
    updated_at: new Date().toISOString(),
  };

  const { data, error } = await db
    .from("user_stats")
    .update(next)
    .eq("user_id", userId)
    .select("user_id, matches_played, wins, losses")
    .single();
  if (error) throw error;
  return formatStats(data as StatsRow);
}

function routePath(req: Request): string {
  const url = new URL(req.url);
  let path = url.pathname;
  if (path.startsWith(`/${FUNCTION_SLUG}`)) path = path.slice(FUNCTION_SLUG.length + 1) || "/";
  return path.replace(/\/$/, "") || "/";
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: corsHeaders });

  try {
    const path = routePath(req);

    if (req.method === "GET" && path === "/health") {
      const { error } = await db.from("users").select("id", { count: "exact", head: true });
      if (error) throw error;
      return json({ status: "ok", store: "supabase", project: "kufkgjyeptuzptmegsmf" });
    }

    if (req.method === "POST" && path === "/auth/register") {
      const body = await readJson(req);
      const username = cleanText(body.username, 50);
      const email = cleanText(body.email, 255).toLowerCase();
      const password = String(body.password ?? "");
      const displayName = cleanText(body.displayName || username, 50) || username;

      if (!username || !email || !password) return json({ error: "username, email y password son obligatorios" }, 400);
      if (password.length < 6) return json({ error: "Password debe tener al menos 6 caracteres" }, 400);

      const existingUsername = await db.from("users").select("id").eq("username", username).maybeSingle();
      if (existingUsername.error) throw existingUsername.error;
      const existingEmail = await db.from("users").select("id").eq("email", email).maybeSingle();
      if (existingEmail.error) throw existingEmail.error;
      if (existingUsername.data || existingEmail.data) return json({ error: "Usuario o email ya existe" }, 409);

      const passwordHash = await hashPassword(password);
      const { data, error } = await db
        .from("users")
        .insert({ username, email, password_hash: passwordHash, display_name: displayName })
        .select("id, username, display_name")
        .single();
      if (error) throw error;

      const user = data as UserRow;
      const token = await signToken(user);
      return json({ token, userId: user.id, username: user.username, displayName: user.display_name }, 201);
    }

    if (req.method === "POST" && path === "/auth/login") {
      const body = await readJson(req);
      const username = cleanText(body.username, 255);
      const password = String(body.password ?? "");
      if (!username || !password) return json({ error: "username y password son obligatorios" }, 400);

      let userQuery = await db
        .from("users")
        .select("id, username, display_name, password_hash")
        .eq("username", username)
        .maybeSingle();
      if (userQuery.error) throw userQuery.error;

      if (!userQuery.data) {
        userQuery = await db
          .from("users")
          .select("id, username, display_name, password_hash")
          .eq("email", username.toLowerCase())
          .maybeSingle();
        if (userQuery.error) throw userQuery.error;
      }

      const user = userQuery.data as UserRow | null;
      if (!user?.password_hash || !(await verifyPassword(password, user.password_hash))) {
        return json({ error: "Credenciales invalidas" }, 401);
      }

      await db.from("users").update({ last_login: new Date().toISOString() }).eq("id", user.id);
      const token = await signToken(user);
      return json({ token, userId: user.id, username: user.username, displayName: user.display_name });
    }

    if (req.method === "GET" && path === "/auth/verify") {
      const user = await requireAuth(req);
      return json({ userId: user.id, username: user.username, displayName: user.display_name });
    }

    if (req.method === "GET" && path === "/profile/stats") {
      const user = await requireAuth(req);
      return json(await getStats(user.id));
    }

    if (req.method === "POST" && path === "/profile/match") {
      const user = await requireAuth(req);
      const body = await readJson(req);
      if (typeof body.won !== "boolean") return json({ error: "won debe ser boolean" }, 400);
      return json(await recordMatch(user.id, body.won));
    }

    return json({ error: "Not found" }, 404);
  } catch (err) {
    if (err instanceof Response) {
      const status = err.status || 500;
      try {
        return json(JSON.parse(await err.text()), status);
      } catch {
        return json({ error: "Error de autenticacion" }, status);
      }
    }
    console.error(err);
    return json({ error: "Error interno del servidor" }, 500);
  }
});
