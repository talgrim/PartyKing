// Starts the API for the e2e run on a throw-away database, with the admin account the tests log in as.
//
// The API reads appsettings.Local.json from its content root, after (so over) the environment variables, and
// a developer's copy holds real Spotify secrets and an admin seed. So its content root is an empty directory that
// holds only the committed appsettings, and the tests behave the same on every machine.
//
// The directory is e2e/.work (git-ignored), wiped and recreated on every start. Playwright may kill this process
// without letting it clean up, so the wipe happens at the start; and a fixed path inside the repo keeps the run out
// of the shared temp directory.
import { spawn } from 'node:child_process';
import { copyFileSync, mkdirSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const apiProject = resolve(here, '../../../api/PartyKing.API');

// `E2E_DEMO=1` starts the demo variant (see playwright.demo.config.ts): another port, folder and database, and the
// demo switched on, so it can run after the ordinary suite without sharing anything with it.
const demo = process.env.E2E_DEMO === '1';
const port = demo ? 4300 : 4200;

const workDir = join(here, demo ? '.work-demo' : '.work');
rmSync(workDir, { recursive: true, force: true });
mkdirSync(join(workDir, 'wwwroot'), { recursive: true });
// The API runs in the e2e environment: the same development helpers as Development (CORS, dev personas, no rate
// limits, the seeded random source), but with its own appsettings.e2e.json (it quiets the log, which CI prints in full).
for (const file of ['appsettings.json', 'appsettings.e2e.json']) {
  copyFileSync(join(apiProject, file), join(workDir, file));
}

const child = spawn('dotnet', ['run', '--project', apiProject, '--no-launch-profile'], {
  cwd: workDir,
  stdio: 'inherit',
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'e2e',
    // `dotnet run` would make the project folder the content root, and its appsettings.Local.json the winner
    ASPNETCORE_CONTENTROOT: workDir,
    ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
    ASPNETCORE_WEBROOT: join(workDir, 'wwwroot'),
    PARTYKING_DATABASE_PATH: join(workDir, demo ? 'e2e-demo.db' : 'e2e.db'),
    AdminSettings__Username: 'e2e-admin',
    AdminSettings__Password: 'e2e-admin-password',
    RateLimiting__Disabled: 'true',
    Resistance__Seed: '931902',
    ...(demo ? { DemoSettings__Enabled: 'true' } : {}),
  },
});

for (const signal of ['SIGTERM', 'SIGINT']) {
  process.on(signal, () => child.kill(signal));
}
child.on('exit', (code) => process.exit(code ?? 0));
