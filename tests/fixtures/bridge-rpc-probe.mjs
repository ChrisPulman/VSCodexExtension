import fs from 'node:fs';
import vm from 'node:vm';
import readline from 'node:readline';
import { EventEmitter } from 'node:events';
import { PassThrough, Writable } from 'node:stream';

// Exercise the actual packaged implementation with deterministic local pipes.
const source = fs.readFileSync(process.argv[2], 'utf8');
const implementation = source.slice(source.indexOf('function createJsonRpcClient('), source.indexOf('function emitCodexProgress('));
const context = vm.createContext({ readline, setTimeout, clearTimeout });
vm.runInContext(implementation, context);
const createClient = context.createJsonRpcClient;
function fakeProcess() {
  const child = new EventEmitter();
  child.stdout = new PassThrough();
  child.messages = [];
  child.stdin = new Writable({
    write(chunk, encoding, callback) {
      child.messages.push(JSON.parse(chunk.toString('utf8')));
      callback();
    }
  });
  return child;
}

const child = fakeProcess();
const notifications = [];
const rpc = createClient(child, 1000, message => notifications.push(message));
await rpc.notify('initialized', {});
await rpc.respond(0, { action: 'accept', content: { name: 'Überprüfung 日本語 😀' } });
await rpc.respond('0', { action: 'decline', content: null });
const one = rpc.send({ id: 'one', method: 'test/one' });
const two = rpc.send({ id: 'two', method: 'test/two' });
child.stdout.write('{"id":"two","result":{"value":2}}\n');
child.stdout.write('{"method":"server/event","params":{}}\n');
child.stdout.write('{"id":"one","result":{"value":1}}\n');
const results = await Promise.all([one, two]);
const waiting = rpc.send({ id: 'pending', method: 'test/pending' }).then(() => 'resolved', error => error.message);
child.emit('close', 0);
const closedError = await waiting;
const lateResponse = await rpc.respond(4, {}).then(() => 'resolved', error => error.message);

const failedChild = fakeProcess();
let disconnects = 0;
const failedRpc = createClient(failedChild, 1000, null, () => disconnects++);
const failedWaiting = failedRpc.send({ id: 'failure', method: 'test/failure' }).then(() => 'resolved', error => error.message);
failedChild.emit('error', new Error('simulated process failure'));
const spawnError = await failedWaiting;
failedChild.emit('close', 1);

const timeoutChild = fakeProcess();
const timeoutRpc = createClient(timeoutChild, 5);
const timeoutError = await timeoutRpc.send({ id: 'timeout', method: 'test/timeout' }).then(() => 'resolved', error => error.message);
timeoutRpc.close();

console.log(JSON.stringify({
  notification: child.messages[0],
  numericId: child.messages[1].id,
  stringId: child.messages[2].id,
  unicode: child.messages[1].result.content.name,
  results: results.map(response => response.result.value),
  notificationCount: notifications.length,
  closedError,
  lateResponse,
  spawnError,
  disconnects,
  timeoutError
}));
