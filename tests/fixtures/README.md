# Native MCP acceptance fixtures

`native-form-bridge.mjs` replaces the extension's bridge for deterministic WPF routing checks. It is not a live Codex/MCP test. Clear the Bridge script path override afterward.

`live-mcp-server.mjs` is a real stdio MCP server. It exposes only `request_test_form`, which requests primitive form inputs and records the action/typed response in `%TEMP%/VSCodex-validation/live-mcp-results.jsonl`. Its elicitation request ID is numeric zero. It executes no commands and does not edit workspace files.

To repeat live acceptance, create the results directory, back up Codex config, then add a temporary uniquely named MCP server with `command` pointing to Node and `args` containing the absolute path to `live-mcp-server.mjs`. Use TOML literal strings or forward slashes for Windows paths. Restart the isolated Visual Studio test instance so its app-server discovers the server. Keep the extension's normal bundled bridge and Codex app-server transport.

Ask Codex to call this one tool once without other tools or file edits. Accept the tool-use authorization form first. In the actual test form, verify empty required input is rejected; supply Unicode display text; keep integer/boolean/enum defaults; leave optional note omitted. Accept and inspect the server's JSONL result. Repeat for Decline and Cancel, accepting the tool-use authorization each time. The real server must receive `accept`, `decline` and `cancel`; an authorization decline before tool execution does not test the fixture's elicitation.

After testing, close the isolated IDE, remove only the temporary server block while preserving unrelated config edits, and verify its process tree exits. Never leave a bridge override or enabled acceptance automation behind. Record exact IDE/build versions and screenshots in the validation report.
