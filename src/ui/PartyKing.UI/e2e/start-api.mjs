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

const workDir = join(here, '.work');
rmSync(workDir, { recursive: true, force: true });
mkdirSync(join(workDir, 'wwwroot'), { recursive: true });
for (const file of ['appsettings.json', 'appsettings.Development.json']) {
  copyFileSync(join(apiProject, file), join(workDir, file));
}

const child = spawn('dotnet', ['run', '--project', apiProject, '--no-launch-profile'], {
  cwd: workDir,
  stdio: 'inherit',
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    // `dotnet run` would make the project folder the content root, and its appsettings.Local.json the winner
    ASPNETCORE_CONTENTROOT: workDir,
    ASPNETCORE_URLS: 'http://127.0.0.1:4200',
    ASPNETCORE_WEBROOT: join(workDir, 'wwwroot'),
    PARTYKING_DATABASE_PATH: join(workDir, 'e2e.db'),
    AdminSettings__Username: 'e2e-admin',
    AdminSettings__Password: 'e2e-admin-password',
    RateLimiting__Disabled: 'true',
    Resistance__Seed: '1',
  },
});

for (const signal of ['SIGTERM', 'SIGINT']) {
  process.on(signal, () => child.kill(signal));
}
child.on('exit', (code) => process.exit(code ?? 0));
