// Deterministic bridge replacement for native Visual Studio acceptance testing.
// Select temporarily through BridgeScriptPath; restore the real bridge after testing.
// This tests native UI/C# routing, not a live MCP server or Codex service.
import readline from 'node:readline';
import fs from 'node:fs';
import path from 'node:path';

if (process.argv.includes('--check')) {
  console.log('Native acceptance fixture available');
  process.exit(0);
}

const emit = value => process.stdout.write(JSON.stringify(value) + '\n');
let active;
const schema = {
  type: 'object',
  required: ['name', 'count', 'enabled', 'choice'],
  properties: {
    name: { type: 'string', title: 'Display name', minLength: 2 },
    count: { type: 'integer', title: 'Count', default: 3, minimum: 1 },
    enabled: { type: 'boolean', title: 'Enabled', default: true },
    choice: { type: 'string', title: 'Choice', enum: ['safe', 'fast'], default: 'safe' },
    note: { type: 'string', title: 'Optional note' }
  }
};
readline.createInterface({ input: process.stdin }).on('line', line => {
  const request = JSON.parse(line);
  if (request.command === 'getModels') {
    // Leave saved model choices intact; discovery behavior has a separate fixture.
    emit({ id: request.id, type: 'response', result: { data: [] } });
  } else if (request.command === 'getRateLimits') {
    emit({ id: request.id, type: 'response', result: {} });
  } else if (request.command === 'respondServerRequest') {
    const result = { requestId: request.requestId, response: request.result };
    fs.appendFileSync(path.join(process.env.TEMP, 'VSCodex-validation', 'native-form-results.jsonl'), JSON.stringify(result) + '\n', 'utf8');
    emit({ id: request.id, type: 'response', result: {} });
    emit({ id: active.id, type: 'response', result: { threadId: 'native-fixture-thread', finalResponse: JSON.stringify(result) } });
    active = undefined;
  } else if (request.command === 'interrupt' || request.command === 'cancel') {
    emit({ id: request.id, type: 'response', result: {} });
  } else {
    active = request;
    emit({ type: 'approval-request', requestId: 0, method: 'mcpServer/elicitation/request', message: 'Native fixture: type-safe form with numeric request ID 0', params: { mode: 'form', message: 'Enter a display name, then Accept; or exercise Decline/Cancel.', requestedSchema: schema } });
  }
});
emit({ type: 'ready', message: 'Native acceptance fixture ready' });
