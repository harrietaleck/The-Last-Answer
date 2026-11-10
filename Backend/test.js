// Smoke test: starts the server on a spare port and checks the contract.  Run: node test.js
'use strict';

const { spawn } = require('child_process');
const assert = require('assert');

const PORT = 18787;
const base = `http://127.0.0.1:${PORT}`;

function start(env = {}) {
  const p = spawn(process.execPath, ['server.js'], { cwd: __dirname, env: { ...process.env, PORT: String(PORT), TLA_FAULT: '', ...env } });
  return new Promise((resolve, reject) => {
    p.stdout.on('data', d => { if (String(d).includes('dialogue backend')) resolve(p); });
    p.stderr.on('data', d => process.stderr.write(d));
    p.on('exit', code => reject(new Error('server exited ' + code)));
  });
}

const request = {
  schemaVersion: 1,
  requestId: 'abc123',
  limits: { maxReplyChars: 320, maxNarrationChars: 120 },
  suspect: { name: 'Elliot Marlowe', temperament: 'Defensive', composure: 60, composureLevel: 'Composed' },
  turn: { number: 1, playerInput: 'Where were you?', replyKind: 'TopicAnswer', topic: { id: 'whereabouts', name: 'Whereabouts', stance: 'Lie' } },
  allowed: { baseline: 'I was at home. All evening.', baselineNarration: '' },
  history: []
};

async function post(body) {
  const r = await fetch(base + '/v1/dialogue', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: typeof body === 'string' ? body : JSON.stringify(body) });
  return { status: r.status, json: await r.json().catch(() => null) };
}

(async () => {
  let server = await start();
  try {
    const health = await (await fetch(base + '/health')).json();
    assert.strictEqual(health.ok, true);

    const ok = await post(request);
    assert.strictEqual(ok.status, 200);
    assert.strictEqual(ok.json.requestId, 'abc123');
    assert.strictEqual(ok.json.schemaVersion, 1);
    assert.strictEqual(ok.json.text, 'I was at home. All evening.');

    assert.strictEqual((await post('{not json')).status, 400);
    assert.strictEqual((await post({ ...request, schemaVersion: 2 })).status, 400);
    assert.strictEqual((await post({ ...request, allowed: undefined })).status, 400);
    assert.strictEqual((await post({ ...request, history: new Array(50).fill({ speaker: 'Suspect', text: 'x' }) })).status, 400);
    console.log('PASS contract');
  } finally {
    server.kill();
  }

  server = await start({ TLA_FAULT: 'error' });
  try {
    assert.strictEqual((await post(request)).status, 500);
    console.log('PASS fault=error');
  } finally {
    server.kill();
  }
  console.log('All backend tests passed.');
})().catch(e => { console.error('FAIL', e); process.exit(1); });
