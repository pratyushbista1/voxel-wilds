import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { createGameServer, ROOT, validateSave } from '../server.mjs';
const fixture = () => ({
  version: 1,
  id: 'test-world',
  name: 'Test world',
  seed: 'test',
  mode: 'survival',
  edits: [['-1,40,0', 7]],
  player: { x: 0, y: 35, z: 0, yaw: 0, pitch: 0, health: 20, hunger: 20 },
  inventory: { 5: 1 },
  hotbar: [5, 0, 0, 0, 0, 0, 0, 0, 0],
  time: 400,
  playTime: 1,
});

test('server saves atomically, lists worlds, retains backup and restores a damaged primary', async (t) => {
  await mkdir(path.join(ROOT, '.cache'), { recursive: true });
  const saveDir = await mkdtemp(path.join(ROOT, '.cache', 'save-test-')),
    server = createGameServer({ saveDir });
  await new Promise((r) => server.listen(0, '127.0.0.1', r));
  t.after(() => server.close());
  const base = `http://127.0.0.1:${server.address().port}`;
  const first = await fetch(base + '/api/worlds/test-world', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(fixture()),
  });
  assert.equal(first.status, 200);
  const modified = fixture();
  modified.edits.push(['1,40,0', 10]);
  const second = await fetch(base + '/api/worlds/test-world', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(modified),
  });
  assert.equal(second.status, 200);
  assert.equal(
    JSON.parse(await readFile(path.join(saveDir, 'test-world.bak'), 'utf8')).edits.length,
    1
  );
  assert.equal((await (await fetch(base + '/api/worlds')).json())[0].name, 'Test world');
  assert.equal((await (await fetch(base + '/api/worlds/test-world')).json()).edits.length, 2);
  await writeFile(path.join(saveDir, 'test-world.json'), 'incomplete save');
  assert.equal((await (await fetch(base + '/api/worlds/test-world')).json()).edits.length, 1);
  assert.equal((await fetch(base + '/saves/test-world.json')).status, 403);
  assert.equal((await fetch(base + '/tools/blender.exe')).status, 403);
  assert.equal(
    (
      await fetch(base + '/api/worlds/test-world', {
        method: 'PUT',
        headers: { Origin: 'https://unrelated.example', 'Content-Type': 'application/json' },
        body: JSON.stringify(fixture()),
      })
    ).status,
    403
  );
  assert.equal(
    (
      await fetch(base + '/api/worlds/test-world', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: '{}',
      })
    ).status,
    400
  );
  assert.equal((await fetch(base + '/')).status, 200);
});
test('save validation rejects non-finite player values and malformed edits', () => {
  const bad = fixture();
  bad.player.x = NaN;
  assert.throws(() => validateSave(bad, bad.id));
  const edit = fixture();
  edit.edits = [['1,0,1', 0]];
  assert.throws(() => validateSave(edit, edit.id));
  const id = fixture();
  assert.throws(() => validateSave(id, '../escape'));
});
