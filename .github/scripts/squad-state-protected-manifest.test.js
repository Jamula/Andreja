'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const net = require('node:net');
const path = require('node:path');
const { once } = require('node:events');
const test = require('node:test');

const {
  ManifestError,
  captureRecords,
  compareWithBaseline,
  writeBaseline,
} = require('./squad-state-protected-manifest');

let sequence = 0;

function fixture(t) {
  sequence += 1;
  const parent = path.resolve(__dirname, '..', '..');
  const name = `.squad-state-manifest-test-${process.pid}-${sequence}`;
  const root = path.join(parent, name);
  const baseline = path.join(parent, `${name}.json`);
  fs.mkdirSync(path.join(root, '.squad', 'agents'), { recursive: true });
  t.after(() => {
    fs.rmSync(root, { recursive: true, force: true });
    fs.rmSync(baseline, { force: true });
  });
  return { baseline, root };
}

function protectedFile(root, relativePath, contents = 'state') {
  const file = path.join(root, ...relativePath.split('/'));
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, contents);
  return file;
}

test('reports an unchanged protected-state manifest', (t) => {
  const { baseline, root } = fixture(t);
  protectedFile(root, '.squad/decisions.md');
  writeBaseline(root, baseline);
  assert.deepEqual(compareWithBaseline(root, baseline), []);
});

test('does not remove or modify a pre-existing baseline', (t) => {
  const { baseline, root } = fixture(t);
  const expected = Buffer.from([0x00, 0xff, 0x53, 0x71, 0x75, 0x61, 0x64, 0x0a]);
  fs.writeFileSync(baseline, expected);

  assert.throws(
    () => writeBaseline(root, baseline),
    error => error instanceof ManifestError && error.message === 'baseline could not be created',
  );
  assert.deepEqual(fs.readFileSync(baseline), expected);
});

test('detects a created protected path', (t) => {
  const { baseline, root } = fixture(t);
  writeBaseline(root, baseline);
  protectedFile(root, '.squad/log/probe.md');
  assert.ok(compareWithBaseline(root, baseline).includes('.squad/log/probe.md'));
});

test('detects a same-size protected-file modification by timestamps', (t) => {
  const { baseline, root } = fixture(t);
  const file = protectedFile(root, '.squad/decisions.md', 'before');
  writeBaseline(root, baseline);
  fs.writeFileSync(file, 'after!');
  const future = new Date(Date.now() + 10_000);
  fs.utimesSync(file, future, future);
  assert.deepEqual(compareWithBaseline(root, baseline), ['.squad/decisions.md']);
});

test('detects a deleted protected path', (t) => {
  const { baseline, root } = fixture(t);
  const file = protectedFile(root, '.squad/casting/history.json');
  writeBaseline(root, baseline);
  fs.rmSync(file);
  assert.ok(compareWithBaseline(root, baseline).includes('.squad/casting/history.json'));
});

test('includes protected archived agent history', (t) => {
  const { baseline, root } = fixture(t);
  writeBaseline(root, baseline);
  protectedFile(root, '.squad/agents/data/history-archive.md');
  assert.deepEqual(
    compareWithBaseline(root, baseline),
    ['.squad/agents/data/history-archive.md'],
  );
});

test('excludes unprotected Squad files', (t) => {
  const { baseline, root } = fixture(t);
  writeBaseline(root, baseline);
  protectedFile(root, '.squad/team.md');
  protectedFile(root, '.squad/agents/data/charter.md');
  assert.deepEqual(compareWithBaseline(root, baseline), []);
});

test('capture does not open protected file contents', (t) => {
  const { root } = fixture(t);
  protectedFile(root, '.squad/decisions.md');
  const original = fs.readFileSync;
  fs.readFileSync = () => {
    throw new Error('file contents must not be opened');
  };
  try {
    assert.doesNotThrow(() => captureRecords(root));
  } finally {
    fs.readFileSync = original;
  }
});

test('fails closed on a symbolic link in protected state', (t) => {
  const { root } = fixture(t);
  const target = path.join(root, 'unprotected-target');
  const link = path.join(root, '.squad', 'log');
  fs.mkdirSync(target);
  try {
    fs.symlinkSync(target, link, process.platform === 'win32' ? 'junction' : 'dir');
  } catch (error) {
    if (error && ['EPERM', 'EACCES', 'ENOTSUP'].includes(error.code)) {
      t.skip(`symbolic links unavailable: ${error.code}`);
      return;
    }
    throw error;
  }
  assert.throws(
    () => captureRecords(root),
    error => error instanceof ManifestError && /symbolic link or reparse point/u.test(error.message),
  );
});

test('fails closed on a non-regular protected entry', {
  skip: process.platform === 'win32' ? 'Unix-domain filesystem sockets are not portable to Windows' : false,
}, async (t) => {
  const { root } = fixture(t);
  const socket = path.join(root, '.squad', 'log', 'state.sock');
  fs.mkdirSync(path.dirname(socket), { recursive: true });
  const server = net.createServer();
  server.listen(socket);
  await once(server, 'listening');
  try {
    assert.throws(
      () => captureRecords(root),
      error => error instanceof ManifestError && /unsupported type/u.test(error.message),
    );
  } finally {
    server.close();
    await once(server, 'close');
  }
});
