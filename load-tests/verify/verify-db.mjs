#!/usr/bin/env node
/*
 * Convenience runner for verify/verify.sql. It shells out to `psql`, so no npm
 * dependency is needed. Point it at the database with DATABASE_URL, or let it
 * exec into a local docker container.
 *
 *   node verify/verify-db.mjs                       # uses $DATABASE_URL
 *   node verify/verify-db.mjs <game-session-uuid>   # scope to one game
 *   PSQL_DOCKER=kahoot-loadtest-db node verify/verify-db.mjs   # docker exec psql
 *
 * Exit code is non-zero if psql reports any violation rows.
 */
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
const sqlFile = path.join(here, 'verify.sql');
const game = process.argv[2];

const dockerName = process.env.PSQL_DOCKER;
const dbUrl = process.env.DATABASE_URL || 'postgres://postgres:postgres@localhost:5433/kahoot';

let cmd;
let args;
const vArg = game ? ['-v', `game='${game}'`] : [];

if (dockerName) {
  // stream the file into psql inside the container
  cmd = 'docker';
  args = ['exec', '-i', dockerName, 'psql', '-U', 'postgres', '-d', 'kahoot', '-v', 'ON_ERROR_STOP=1', ...vArg, '-f', '-'];
  const sql = spawnSync('cat', [sqlFile], { encoding: 'utf8' });
  const res = spawnSync(cmd, args, { input: sql.stdout, stdio: ['pipe', 'inherit', 'inherit'] });
  process.exit(res.status || 0);
} else {
  cmd = 'psql';
  args = [dbUrl, '-v', 'ON_ERROR_STOP=1', ...vArg, '-f', sqlFile];
  const res = spawnSync(cmd, args, { stdio: 'inherit' });
  if (res.error) {
    console.error(`Could not run psql (${res.error.message}). Install psql or set PSQL_DOCKER=<container>.`);
    process.exit(2);
  }
  process.exit(res.status || 0);
}
