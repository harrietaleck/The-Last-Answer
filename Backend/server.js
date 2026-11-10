// THE LAST ANSWER: reference dialogue backend.
//
// The game never talks to an AI service directly. It POSTs a filtered dialogue request here;
// this server builds the prompt, calls the AI service with a secret key that lives ONLY on the
// server (environment variable), checks the result and returns {schemaVersion, requestId, text, narration}.
//
// No dependencies: plain Node.js 18+.  Run:  node server.js   (or: npm start)
// Configuration is read from environment variables (see .env.example). Nothing secret is in this file.

'use strict';

const http = require('http');
const { buildPrompt } = require('./prompt');

const SCHEMA_VERSION = 1;
const PORT = parseInt(process.env.PORT || '8787', 10);
const HOST = process.env.HOST || '127.0.0.1';             // local only by default
const PROVIDER = (process.env.TLA_PROVIDER || 'stub').toLowerCase();
const FAULT = (process.env.TLA_FAULT || '').toLowerCase(); // testing: slow | error | invalid | wrongid | invent | ratelimit
const MAX_BODY_BYTES = 64 * 1024;
const RATE_LIMIT_PER_MINUTE = parseInt(process.env.TLA_RATE_LIMIT_PER_MINUTE || '30', 10);
const HARD_MAX_REPLY = 1000;
const HARD_MAX_NARRATION = 300;

if (!/^[a-z0-9_-]+$/.test(PROVIDER)) throw new Error('TLA_PROVIDER may only contain letters, digits, - and _');
const provider = require(`./providers/${PROVIDER}`);

// ---------------------------------------------------------------------------- rate limiting
const hits = new Map(); // ip -> [timestamps]
function rateLimited(ip) {
  const now = Date.now();
  const recent = (hits.get(ip) || []).filter(t => now - t < 60_000);
  recent.push(now);
  hits.set(ip, recent);
  return recent.length > RATE_LIMIT_PER_MINUTE;
}
setInterval(() => { const now = Date.now(); for (const [ip, ts] of hits) if (!ts.some(t => now - t < 60_000)) hits.delete(ip); }, 60_000).unref();

// ---------------------------------------------------------------------------- request validation
function isStr(v, max) { return typeof v === 'string' && v.length <= max; }

function validateRequest(r) {
  if (!r || typeof r !== 'object') return 'body must be a JSON object';
  if (r.schemaVersion !== SCHEMA_VERSION) return `schemaVersion must be ${SCHEMA_VERSION}`;
  if (!isStr(r.requestId, 64) || !r.requestId) return 'requestId missing';
  if (!r.suspect || !isStr(r.suspect.name, 80)) return 'suspect.name missing';
  if (!r.turn || !isStr(r.turn.replyKind, 40)) return 'turn.replyKind missing';
  if (r.turn.playerInput !== undefined && !isStr(r.turn.playerInput, 300)) return 'turn.playerInput too long';
  if (!r.allowed || !isStr(r.allowed.baseline, 1200)) return 'allowed.baseline missing';
  if (r.history !== undefined && (!Array.isArray(r.history) || r.history.length > 40)) return 'history must be an array of at most 40 lines';
  for (const h of r.history || []) if (!h || !isStr(h.text, 1200) || !isStr(h.speaker, 20)) return 'bad history line';
  return null;
}

function limitsOf(r) {
  const l = r.limits || {};
  const reply = Number.isInteger(l.maxReplyChars) ? l.maxReplyChars : 320;
  const narration = Number.isInteger(l.maxNarrationChars) ? l.maxNarrationChars : 120;
  return { maxReplyChars: Math.min(Math.max(reply, 40), HARD_MAX_REPLY), maxNarrationChars: Math.min(Math.max(narration, 0), HARD_MAX_NARRATION) };
}

// ---------------------------------------------------------------------------- output clean-up
function clean(s, max) {
  if (typeof s !== 'string') return '';
  s = s.replace(/<[^>]{0,64}>/g, '').replace(/[\u0000-\u001f]/g, ' ').replace(/\s+/g, ' ').trim();
  if (s.length <= max) return s;
  const cut = s.slice(0, max);
  const end = Math.max(cut.lastIndexOf('.'), cut.lastIndexOf('!'), cut.lastIndexOf('?'));
  return end > 0 ? cut.slice(0, end + 1) : '';
}

// ---------------------------------------------------------------------------- HTTP
function send(res, status, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Content-Length': Buffer.byteLength(body), 'Cache-Control': 'no-store' });
  res.end(body);
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let size = 0;
    const chunks = [];
    req.on('data', c => {
      size += c.length;
      if (size > MAX_BODY_BYTES) { reject(Object.assign(new Error('too large'), { status: 413 })); req.destroy(); return; }
      chunks.push(c);
    });
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
    req.on('error', reject);
  });
}

async function handleDialogue(req, res) {
  const ip = req.socket.remoteAddress || 'unknown';
  if (rateLimited(ip) || FAULT === 'ratelimit') return send(res, 429, { error: 'rate_limited' });

  let request;
  try {
    request = JSON.parse(await readBody(req));
  } catch (e) {
    return send(res, e.status || 400, { error: e.status === 413 ? 'too_large' : 'invalid_json' });
  }

  const problem = validateRequest(request);
  if (problem) return send(res, 400, { error: 'invalid_request', detail: problem });

  const limits = limitsOf(request);
  const prompt = buildPrompt(request, limits);

  // ---- testing faults (TLA_FAULT), to watch the game fall back to offline dialogue
  if (FAULT === 'slow') await new Promise(r => setTimeout(r, 30_000));
  if (FAULT === 'error') return send(res, 500, { error: 'simulated_failure' });
  if (FAULT === 'invalid') { res.writeHead(200, { 'Content-Type': 'application/json' }); return res.end('{"text": "broken'); }

  let reply;
  try {
    reply = await provider.generate({ request, prompt, limits });
  } catch (e) {
    console.error(`[dialogue] provider "${PROVIDER}" failed: ${e.message}`);
    return send(res, 502, { error: 'provider_failed' });
  }

  let text = clean(reply && reply.text, limits.maxReplyChars);
  const narration = clean(reply && reply.narration, limits.maxNarrationChars);
  if (FAULT === 'invent') text = 'I was at the Riverside Hotel until 11:45, ask Gregory.';
  if (!text) return send(res, 502, { error: 'empty_reply' });

  send(res, 200, {
    schemaVersion: SCHEMA_VERSION,
    requestId: FAULT === 'wrongid' ? 'not-the-same-id' : request.requestId,
    text,
    narration
  });
}

const server = http.createServer(async (req, res) => {
  try {
    if (req.method === 'GET' && req.url === '/health') return send(res, 200, { ok: true, schemaVersion: SCHEMA_VERSION, provider: PROVIDER });
    if (req.method === 'POST' && req.url === '/v1/dialogue') return await handleDialogue(req, res);
    send(res, 404, { error: 'not_found' });
  } catch (e) {
    console.error('[server]', e);
    if (!res.headersSent) send(res, 500, { error: 'internal' });
  }
});

server.requestTimeout = 20_000;
server.listen(PORT, HOST, () => {
  console.log(`The Last Answer dialogue backend on http://${HOST}:${PORT}  (provider: ${PROVIDER}${FAULT ? ', fault: ' + FAULT : ''})`);
});
