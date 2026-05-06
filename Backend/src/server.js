const express = require('express');
const fs = require('fs');
const path = require('path');
const { Pool } = require('pg');
const bcrypt = require('bcrypt');
const jwt = require('jsonwebtoken');

const app = express();
app.use(express.json());
app.use((err, req, res, next) => {
  if (err instanceof SyntaxError && err.status === 400 && 'body' in err) {
    return res.status(400).json({ error: 'JSON invalido' });
  }
  next(err);
});

const pool = new Pool({
  host: process.env.DB_HOST || 'localhost',
  port: parseInt(process.env.DB_PORT || '5432'),
  database: process.env.DB_NAME || 'frentepartido',
  user: process.env.DB_USER || 'fpuser',
  password: process.env.DB_PASSWORD || 'fppass123',
});

const JWT_SECRET = process.env.JWT_SECRET || 'dev-secret';
const SALT_ROUNDS = 10;
const STORE_MODE = (process.env.AUTH_STORE || 'auto').toLowerCase();
const DATA_FILE = process.env.AUTH_DATA_FILE || path.join(__dirname, '..', 'data', 'users.json');

let checkedPostgres = false;
let useFileStore = STORE_MODE === 'file';
let fileStore;

function loadFileStore() {
  if (fileStore) return fileStore;

  fs.mkdirSync(path.dirname(DATA_FILE), { recursive: true });
  if (!fs.existsSync(DATA_FILE)) {
    fs.writeFileSync(DATA_FILE, JSON.stringify({ nextId: 1, users: [] }, null, 2));
  }

  fileStore = JSON.parse(fs.readFileSync(DATA_FILE, 'utf8'));
  fileStore.nextId = fileStore.nextId || 1;
  fileStore.users = Array.isArray(fileStore.users) ? fileStore.users : [];
  return fileStore;
}

function saveFileStore() {
  fs.writeFileSync(DATA_FILE, JSON.stringify(fileStore, null, 2));
}

async function ensureStore() {
  if (useFileStore || checkedPostgres) return;
  checkedPostgres = true;

  try {
    await pool.query('SELECT 1');
  } catch (err) {
    if (STORE_MODE === 'postgres') {
      throw err;
    }

    useFileStore = true;
    loadFileStore();
    console.warn(`[Auth API] Postgres unavailable. Using local JSON store: ${DATA_FILE}`);
  }

  if (!useFileStore) {
    await ensurePostgresSchema();
  }
}

async function ensurePostgresSchema() {
  await pool.query(`
    CREATE TABLE IF NOT EXISTS user_stats (
      user_id INTEGER PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
      matches_played INTEGER NOT NULL DEFAULT 0,
      wins INTEGER NOT NULL DEFAULT 0,
      losses INTEGER NOT NULL DEFAULT 0,
      updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    )
  `);
}

async function findExistingUser(username, email) {
  await ensureStore();

  if (useFileStore) {
    const store = loadFileStore();
    return store.users.find(u => u.username === username || u.email === email) || null;
  }

  const result = await pool.query(
    'SELECT id FROM users WHERE username = $1 OR email = $2',
    [username, email]
  );
  return result.rows[0] || null;
}

async function findLoginUser(identifier) {
  await ensureStore();

  if (useFileStore) {
    const store = loadFileStore();
    return store.users.find(u => u.username === identifier || u.email === identifier) || null;
  }

  const result = await pool.query(
    'SELECT id, username, display_name, password_hash FROM users WHERE username = $1 OR email = $1',
    [identifier]
  );
  return result.rows[0] || null;
}

async function findUserById(id) {
  await ensureStore();

  if (useFileStore) {
    const store = loadFileStore();
    return store.users.find(u => u.id === id) || null;
  }

  const result = await pool.query(
    'SELECT id, username, display_name FROM users WHERE id = $1',
    [id]
  );
  return result.rows[0] || null;
}

async function insertUser(username, email, passwordHash, displayName) {
  await ensureStore();

  if (useFileStore) {
    const store = loadFileStore();
    const user = {
      id: store.nextId++,
      username,
      email,
      password_hash: passwordHash,
      display_name: displayName || username,
      created_at: new Date().toISOString(),
      last_login: null,
    };
    store.users.push(user);
    saveFileStore();
    return user;
  }

  const result = await pool.query(
    `INSERT INTO users (username, email, password_hash, display_name)
     VALUES ($1, $2, $3, $4)
     RETURNING id, username, display_name`,
    [username, email, passwordHash, displayName || username]
  );
  return result.rows[0];
}

async function updateLastLogin(userId) {
  await ensureStore();

  if (useFileStore) {
    const store = loadFileStore();
    const user = store.users.find(u => u.id === userId);
    if (user) {
      user.last_login = new Date().toISOString();
      saveFileStore();
    }
    return;
  }

  await pool.query('UPDATE users SET last_login = CURRENT_TIMESTAMP WHERE id = $1', [userId]);
}

function emptyStats(userId) {
  return {
    user_id: userId,
    matches_played: 0,
    wins: 0,
    losses: 0,
  };
}

function rankFor(matches, wins, winRate) {
  if (matches <= 0) return 'SIN RANGO';
  if (wins >= 30 && winRate >= 70) return 'ELITE';
  if (wins >= 18 && winRate >= 60) return 'ORO';
  if (wins >= 10 && winRate >= 50) return 'PLATA';
  if (wins >= 4) return 'BRONCE';
  return 'RECLUTA';
}

function formatStats(row) {
  const matchesPlayed = Number(row.matches_played || 0);
  const wins = Number(row.wins || 0);
  const losses = Number(row.losses || 0);
  const winRate = matchesPlayed > 0 ? (wins * 100) / matchesPlayed : 0;
  return {
    matchesPlayed,
    wins,
    losses,
    winRate,
    rank: rankFor(matchesPlayed, wins, winRate),
  };
}

async function getUserStats(userId) {
  await ensureStore();

  if (useFileStore) {
    const store = loadFileStore();
    const user = store.users.find(u => u.id === userId);
    if (!user) return null;
    user.stats = user.stats || { matches_played: 0, wins: 0, losses: 0 };
    saveFileStore();
    return formatStats(user.stats);
  }

  const result = await pool.query(
    `INSERT INTO user_stats (user_id)
     VALUES ($1)
     ON CONFLICT (user_id) DO NOTHING
     RETURNING user_id, matches_played, wins, losses`,
    [userId]
  );

  if (result.rows[0]) return formatStats(result.rows[0]);

  const stats = await pool.query(
    'SELECT user_id, matches_played, wins, losses FROM user_stats WHERE user_id = $1',
    [userId]
  );
  return formatStats(stats.rows[0] || emptyStats(userId));
}

async function recordUserMatch(userId, won) {
  await ensureStore();

  if (useFileStore) {
    const store = loadFileStore();
    const user = store.users.find(u => u.id === userId);
    if (!user) return null;
    user.stats = user.stats || { matches_played: 0, wins: 0, losses: 0 };
    user.stats.matches_played += 1;
    if (won) user.stats.wins += 1;
    else user.stats.losses += 1;
    saveFileStore();
    return formatStats(user.stats);
  }

  const result = await pool.query(
    `INSERT INTO user_stats (user_id, matches_played, wins, losses, updated_at)
     VALUES ($1, 1, $2, $3, CURRENT_TIMESTAMP)
     ON CONFLICT (user_id) DO UPDATE SET
       matches_played = user_stats.matches_played + 1,
       wins = user_stats.wins + EXCLUDED.wins,
       losses = user_stats.losses + EXCLUDED.losses,
       updated_at = CURRENT_TIMESTAMP
     RETURNING user_id, matches_played, wins, losses`,
    [userId, won ? 1 : 0, won ? 0 : 1]
  );
  return formatStats(result.rows[0]);
}

async function requireAuth(req, res, next) {
  const authHeader = req.headers.authorization;
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    return res.status(401).json({ error: 'Token no proporcionado' });
  }

  try {
    const token = authHeader.split(' ')[1];
    const decoded = jwt.verify(token, JWT_SECRET);
    const user = await findUserById(decoded.userId);

    if (!user) {
      return res.status(401).json({ error: 'Usuario no encontrado' });
    }

    req.user = user;
    next();
  } catch (err) {
    return res.status(401).json({ error: 'Token invalido o expirado' });
  }
}

app.get('/health', async (req, res) => {
  await ensureStore();
  res.json({ status: 'ok', store: useFileStore ? 'file' : 'postgres' });
});

app.post('/auth/register', async (req, res) => {
  const { username, email, password, displayName } = req.body;

  if (!username || !email || !password) {
    return res.status(400).json({ error: 'username, email y password son obligatorios' });
  }

  if (password.length < 6) {
    return res.status(400).json({ error: 'Password debe tener al menos 6 caracteres' });
  }

  try {
    const existing = await findExistingUser(username, email);

    if (existing) {
      return res.status(409).json({ error: 'Usuario o email ya existe' });
    }

    const passwordHash = await bcrypt.hash(password, SALT_ROUNDS);
    const user = await insertUser(username, email, passwordHash, displayName || username);
    const token = jwt.sign(
      { userId: user.id, username: user.username },
      JWT_SECRET,
      { expiresIn: '7d' }
    );

    res.status(201).json({
      token,
      userId: user.id,
      username: user.username,
      displayName: user.display_name,
    });
  } catch (err) {
    console.error('Register error:', err);
    res.status(500).json({ error: 'Error interno del servidor' });
  }
});

app.post('/auth/login', async (req, res) => {
  const { username, password } = req.body;

  if (!username || !password) {
    return res.status(400).json({ error: 'username y password son obligatorios' });
  }

  try {
    const user = await findLoginUser(username);

    if (!user) {
      return res.status(401).json({ error: 'Credenciales invalidas' });
    }

    const valid = await bcrypt.compare(password, user.password_hash);

    if (!valid) {
      return res.status(401).json({ error: 'Credenciales invalidas' });
    }

    await updateLastLogin(user.id);

    const token = jwt.sign(
      { userId: user.id, username: user.username },
      JWT_SECRET,
      { expiresIn: '7d' }
    );

    res.json({
      token,
      userId: user.id,
      username: user.username,
      displayName: user.display_name,
    });
  } catch (err) {
    console.error('Login error:', err);
    res.status(500).json({ error: 'Error interno del servidor' });
  }
});

app.get('/auth/verify', async (req, res) => {
  const authHeader = req.headers.authorization;
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    return res.status(401).json({ error: 'Token no proporcionado' });
  }

  try {
    const token = authHeader.split(' ')[1];
    const decoded = jwt.verify(token, JWT_SECRET);
    const user = await findUserById(decoded.userId);

    if (!user) {
      return res.status(401).json({ error: 'Usuario no encontrado' });
    }

    res.json({
      userId: user.id,
      username: user.username,
      displayName: user.display_name,
    });
  } catch (err) {
    return res.status(401).json({ error: 'Token invalido o expirado' });
  }
});

app.get('/profile/stats', requireAuth, async (req, res) => {
  try {
    const stats = await getUserStats(req.user.id);
    res.json(stats);
  } catch (err) {
    console.error('Stats read error:', err);
    res.status(500).json({ error: 'Error interno del servidor' });
  }
});

app.post('/profile/match', requireAuth, async (req, res) => {
  const { won } = req.body;
  if (typeof won !== 'boolean') {
    return res.status(400).json({ error: 'won debe ser boolean' });
  }

  try {
    const stats = await recordUserMatch(req.user.id, won);
    res.json(stats);
  } catch (err) {
    console.error('Stats write error:', err);
    res.status(500).json({ error: 'Error interno del servidor' });
  }
});

const PORT = process.env.PORT || 3001;
app.listen(PORT, () => {
  console.log(`[Auth API] Running on port ${PORT}`);
});
