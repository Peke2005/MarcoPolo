const fs = require('fs');
const path = require('path');
const { Pool } = require('pg');

const databaseUrl = process.env.DATABASE_URL || process.env.SUPABASE_DB_URL;
if (!databaseUrl) {
  console.error('Falta DATABASE_URL o SUPABASE_DB_URL.');
  process.exit(1);
}

const schemaPath = path.join(__dirname, '..', 'supabase_schema.sql');
const sql = fs.readFileSync(schemaPath, 'utf8');

const pool = new Pool({
  connectionString: databaseUrl,
  ssl: { rejectUnauthorized: false },
  max: 1,
  connectionTimeoutMillis: 10000,
});

(async () => {
  try {
    await pool.query(sql);
    const result = await pool.query("select table_name from information_schema.tables where table_schema = 'public' and table_name in ('users', 'user_stats') order by table_name");
    console.log('Supabase schema OK:', result.rows.map(r => r.table_name).join(', '));
  } finally {
    await pool.end();
  }
})().catch(err => {
  console.error('Supabase schema failed:', err.message);
  process.exit(1);
});
