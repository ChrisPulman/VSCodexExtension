// A real stdio MCP server for native end-to-end acceptance via Codex app-server.
// No tools execute commands, read source files, or change repository contents.
import readline from 'node:readline';
import fs from 'node:fs';
import path from 'node:path';

const send = value => process.stdout.write(JSON.stringify({ jsonrpc: '2.0', ...value }) + '\n');
let pending;
const requestedSchema = {
  type: 'object', required: ['name', 'count', 'enabled', 'choice'],
  properties: {
    name: { type: 'string', title: 'Display name', minLength: 2 },
    count: { type: 'integer', title: 'Count', default: 3, minimum: 1 },
    enabled: { type: 'boolean', title: 'Enabled', default: true },
    choice: { type: 'string', title: 'Choice', enum: ['safe', 'fast'], default: 'safe' },
    note: { type: 'string', title: 'Optional note' }
  }
};
readline.createInterface({ input: process.stdin }).on('line', line => {
  let message;
  try { message = JSON.parse(line); } catch { return; }
  if (message.id === 0 && !message.method && pending) {
    const report = { requestId: message.id, result: message.result, error: message.error };
    fs.appendFileSync(path.join(process.env.TEMP, 'VSCodex-validation', 'live-mcp-results.jsonl'), JSON.stringify(report) + '\n', 'utf8');
    send({ id: pending.id, result: { content: [{ type: 'text', text: JSON.stringify(report) }] } });
    pending = undefined;
    return;
  }
  if (message.method === 'initialize') {
    send({ id: message.id, result: { protocolVersion: message.params.protocolVersion, capabilities: { tools: {} }, serverInfo: { name: 'vscodex-native-acceptance', version: '1.0.0' } } });
  } else if (message.method === 'tools/list') {
    send({ id: message.id, result: { tools: [{ name: 'request_test_form', description: 'Request the native acceptance test form once and return its accepted, declined, or canceled result. No commands or file edits.', inputSchema: { type: 'object', properties: {} } }] } });
  } else if (message.method === 'tools/call' && message.params?.name === 'request_test_form') {
    if (pending) { send({ id: message.id, error: { code: -32600, message: 'A test form is already pending.' } }); return; }
    pending = message;
    send({ id: 0, method: 'elicitation/create', params: { mode: 'form', message: 'Live MCP acceptance: enter Unicode text and keep typed defaults, or Decline/Cancel.', requestedSchema } });
  } else if (message.method === 'ping') {
    send({ id: message.id, result: {} });
  } else if (message.id !== undefined && message.method) {
    send({ id: message.id, error: { code: -32601, message: 'Method not found' } });
  }
});
