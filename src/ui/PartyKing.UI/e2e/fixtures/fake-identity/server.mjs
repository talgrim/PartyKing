// A stand-in for the identity service (auth.talgrim.dev) for the e2e run: in-memory accounts, a seeded admin, the same
// `/api/{register,login,logout,me,session}` contract, and a login and registration page that send the browser back to
// `?next=`. Its session cookie has no Domain, so a browser sends it to every port of 127.0.0.1: the UI (2137), the API
// (4200) and this server see the same login, like the sites under .talgrim.dev do.
//
// `POST /test/webhook/{id}` (test only) makes it deliver an `account.deleted` event to the API's `POST /internal/events`,
// signed like the real service does: HMAC-SHA256 over "{timestamp}.{body}", the exact bytes of the JSON body.
import { createHmac, randomUUID } from 'node:crypto';
import { createServer } from 'node:http';

const port = Number(process.env.IDENTITY_PORT ?? 4400);
const partyKingApi = process.env.PARTYKING_API_URL ?? 'http://127.0.0.1:4200';
const webhookSecret = process.env.IDENTITY_WEBHOOK_SECRET ?? 'e2e-webhook-secret';

const SESSION_COOKIE = 'talgrim.session';
const HINT_COOKIE = 'talgrim-signed-in';

/** @type {Map<string, {id: string, username: string, password: string, role: string, createdAt: string}>} */
const accounts = new Map();
/** @type {Map<string, string>} session token -> account id */
const sessions = new Map();

const addAccount = (username, password, role) => {
  const account = { id: randomUUID(), username, password, role, createdAt: new Date().toISOString() };
  accounts.set(account.id, account);

  return account;
};

addAccount(process.env.IDENTITY_ADMIN_USERNAME ?? 'e2e-admin', process.env.IDENTITY_ADMIN_PASSWORD ?? 'e2e-admin-password', 'Admin');

const findByUsername = (username) =>
  [...accounts.values()].find((account) => account.username.toLowerCase() === username.toLowerCase());

const readCookies = (request) =>
  Object.fromEntries(
    (request.headers.cookie ?? '')
      .split(';')
      .map((part) => part.trim().split('='))
      .filter(([name]) => name),
  );

const accountOf = (request) => {
  const token = readCookies(request)[SESSION_COOKIE];
  const id = token ? sessions.get(token) : undefined;

  return id ? accounts.get(id) : undefined;
};

const readJson = async (request) => {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  try {
    return JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}');
  } catch {
    return {};
  }
};

const cors = (request, response) => {
  const origin = request.headers.origin;
  // Only the local sites, like the real service in Development
  if (origin && /^http:\/\/127\.0\.0\.1:\d+$/.test(origin)) {
    response.setHeader('Access-Control-Allow-Origin', origin);
    response.setHeader('Access-Control-Allow-Credentials', 'true');
    response.setHeader('Access-Control-Allow-Headers', 'content-type, accept');
    response.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    response.setHeader('Vary', 'Origin');
  }
};

const send = (response, status, body, headers = {}) => {
  const isJson = body !== undefined && typeof body !== 'string';
  response.writeHead(status, {
    ...(body === undefined ? {} : { 'Content-Type': isJson ? 'application/json' : 'text/html; charset=utf-8' }),
    ...headers,
  });
  response.end(body === undefined ? undefined : isJson ? JSON.stringify(body) : body);
};

const signIn = (response, account) => {
  const token = randomUUID();
  sessions.set(token, account.id);
  response.setHeader('Set-Cookie', [
    `${SESSION_COOKIE}=${token}; Path=/; HttpOnly; SameSite=Lax`,
    `${HINT_COOKIE}=1; Path=/; SameSite=Lax`,
  ]);
};

const signOut = (request, response) => {
  const token = readCookies(request)[SESSION_COOKIE];
  if (token) sessions.delete(token);
  const expired = 'Path=/; Max-Age=0; SameSite=Lax';
  response.setHeader('Set-Cookie', [`${SESSION_COOKIE}=; ${expired}`, `${HINT_COOKIE}=; ${expired}`]);
};

const credentialsPage = (mode) => `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>${mode === 'login' ? 'Log in' : 'Sign up'}</title></head>
<body>
  <h1>${mode === 'login' ? 'Log in' : 'Sign up'}</h1>
  <form id="form">
    <label>Username <input name="username" autocomplete="username"></label>
    <label>Password <input name="password" type="password" autocomplete="current-password"></label>
    <button type="submit">${mode === 'login' ? 'Log in' : 'Sign up'}</button>
    <p id="error" role="alert"></p>
  </form>
  <script>
    const next = new URLSearchParams(location.search).get('next');
    // Only the local sites, like the real service's allowlist
    const target = next && /^http:\\/\\/127\\.0\\.0\\.1:\\d+\\//.test(next) ? next : '/account';
    document.getElementById('form').addEventListener('submit', async (event) => {
      event.preventDefault();
      const data = Object.fromEntries(new FormData(event.target));
      const response = await fetch('/api/${mode === 'login' ? 'login' : 'register'}', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      });
      if (response.ok) location.href = target;
      else document.getElementById('error').textContent = response.status === 409 ? 'That username is taken' : 'Invalid username or password';
    });
  </script>
</body></html>`;

const simplePage = (title) => `<!doctype html><html lang="en"><head><meta charset="utf-8"><title>${title}</title></head><body><h1>${title}</h1></body></html>`;

/** Delivers `account.deleted` as the real service does; `badSignature` and `ageSeconds` make the call a wrong one. */
const callWebhook = async (id, { badSignature = false, ageSeconds = 0 } = {}) => {
  const timestamp = Math.floor(Date.now() / 1000) - ageSeconds;
  const body = JSON.stringify({
    id: randomUUID(),
    type: 'account.deleted',
    occurredAt: new Date().toISOString(),
    data: { accountId: id },
  });
  const signature = createHmac('sha256', webhookSecret).update(`${timestamp}.${body}`).digest('hex');
  const response = await fetch(`${partyKingApi}/internal/events`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Identity-Timestamp': String(timestamp),
      'X-Identity-Signature': `sha256=${badSignature ? '0'.repeat(64) : signature}`,
    },
    body,
  });

  return response.status;
};

const server = createServer(async (request, response) => {
  cors(request, response);
  const url = new URL(request.url ?? '/', `http://127.0.0.1:${port}`);
  const route = `${request.method} ${url.pathname}`;

  if (request.method === 'OPTIONS') return send(response, 204);

  if (route === 'GET /health') return send(response, 200, 'ok');
  if (route === 'GET /login') return send(response, 200, credentialsPage('login'));
  if (route === 'GET /register') return send(response, 200, credentialsPage('register'));
  if (route === 'GET /account') return send(response, 200, simplePage('Account settings'));
  if (route === 'GET /admin/users') return send(response, 200, simplePage('Registered users'));

  if (route === 'POST /api/register') {
    const { username, password } = await readJson(request);
    if (!username?.trim() || !password || password.length < 8) return send(response, 400, { title: 'Invalid credentials' });
    if (findByUsername(username.trim())) return send(response, 409, { title: 'That username is taken' });
    signIn(response, addAccount(username.trim(), password, 'User'));

    return send(response, 204);
  }

  if (route === 'POST /api/login') {
    const { username, password } = await readJson(request);
    const account = typeof username === 'string' ? findByUsername(username) : undefined;
    if (!account || account.password !== password) return send(response, 401, { title: 'Invalid username or password' });
    signIn(response, account);

    return send(response, 204);
  }

  if (route === 'POST /api/logout') {
    signOut(request, response);

    return send(response, 204);
  }

  if (route === 'GET /api/me') {
    const account = accountOf(request);

    return account
      ? send(response, 200, { id: account.id, username: account.username, role: account.role, createdAt: account.createdAt })
      : send(response, 401, { title: 'Unauthorized' });
  }

  if (route === 'GET /api/session') {
    const account = accountOf(request);
    if (!account) return send(response, 401);
    const role = url.searchParams.get('role');
    if (role && role !== account.role) return send(response, 403);

    return send(response, 200, { id: account.id, username: account.username, role: account.role });
  }

  // Test only: the API's webhook, called like the real service does when an account is deleted
  const webhook = /^\/test\/webhook\/([0-9a-f-]{36})$/.exec(url.pathname);
  if (request.method === 'POST' && webhook) {
    const status = await callWebhook(webhook[1], {
      badSignature: url.searchParams.get('signature') === 'bad',
      ageSeconds: Number(url.searchParams.get('ageSeconds') ?? 0),
    });

    return send(response, 200, { status });
  }

  return send(response, 404, { title: 'Not found' });
});

server.listen(port, '127.0.0.1', () => console.log(`Fake identity service on http://127.0.0.1:${port}`));

for (const signal of ['SIGTERM', 'SIGINT']) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
