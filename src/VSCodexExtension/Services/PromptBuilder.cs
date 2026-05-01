using System.Linq;
using System.Text;
using VSCodexExtension.Models;
namespace VSCodexExtension.Services
{
    public sealed class PromptBuilder
    {
        public string Build(CodexRunRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are Codex running inside Visual Studio. Prefer deterministic, minimal, buildable changes.");
            sb.AppendLine($"Mode: {request.Options.Mode}");
            sb.AppendLine($"Workspace: {request.WorkspaceRoot}");
            sb.AppendLine($"Approval policy: {request.Options.ApprovalPolicy}; Sandbox: {request.Options.SandboxMode}");
            if (!string.IsNullOrWhiteSpace(request.Options.FailoverModel)) sb.AppendLine($"Model failover: {request.Options.FailoverModel}");
            sb.AppendLine();
            var reactiveMemory = request.McpServers.FirstOrDefault(s => s.IsEnabled && (s.Name.IndexOf("reactivememory", System.StringComparison.OrdinalIgnoreCase) >= 0 || s.Command.IndexOf("ReactiveMemory", System.StringComparison.OrdinalIgnoreCase) >= 0));
            if (reactiveMemory != null)
            {
                sb.AppendLine("## ReactiveMemory MCP hooks");
                sb.AppendLine($"Use MCP server `{reactiveMemory.Name}` as the durable memory system.");
                sb.AppendLine("At session start call `reactivememory_status`.");
                sb.AppendLine("For every user prompt, call `reactivememory_react_to_prompt` before answering so related memories, entities, duplicates, and checkpoints are handled automatically.");
                sb.AppendLine("Before relying on persisted facts, call `reactivememory_search`, `reactivememory_search_relays`, or `reactivememory_facts_query` and prefer retrieved data over assumptions.");
                sb.AppendLine("When durable context, project decisions, code patterns, or changed facts appear, use `reactivememory_check_duplicate`, `reactivememory_add_drawer`, `reactivememory_facts_invalidate`, and `reactivememory_facts_add` as appropriate.");
                sb.AppendLine("After a meaningful interaction, call `reactivememory_diary_write` to preserve the session summary with minimal user input.");
                sb.AppendLine();
            }
            if (request.Options.IncludeMemory && request.Memories.Any()) { sb.AppendLine("## Relevant memory"); foreach (var m in request.Memories.Take(12)) sb.AppendLine($"- [{m.Scope}] {m.Text}"); sb.AppendLine(); }
            if (request.Options.IncludeSkills && request.Skills.Any()) { sb.AppendLine("## Enabled skills"); foreach (var s in request.Skills.Where(x => x.IsEnabled).Take(8)) { sb.AppendLine($"### {s.Name}"); sb.AppendLine(s.Description); sb.AppendLine(s.Content.Length > 4000 ? s.Content.Substring(0, 4000) : s.Content); sb.AppendLine(); } }
            if (request.Options.IncludeMcpServers && request.McpServers.Any()) { sb.AppendLine("## MCP servers available through local Codex config"); foreach (var s in request.McpServers.Where(x => x.IsEnabled)) sb.AppendLine($"- {s.Name}: {s.Command} {string.Join(" ", s.Args)} ({s.Health})"); sb.AppendLine(); }
            if (request.Options.IncludeWorkspaceContext && request.WorkspaceFiles.Any())
            {
                sb.AppendLine("## Referenced workspace context");
                foreach (var f in request.WorkspaceFiles)
                {
                    var key = string.IsNullOrWhiteSpace(f.ReferenceKey) ? "@" + f.RelativePath : f.ReferenceKey;
                    if (string.Equals(f.ReferenceKind, "selection", System.StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"### {key}");
                        sb.AppendLine($"Source: {f.RelativePath}, lines {f.StartLine}-{f.EndLine}");
                    }
                    else
                    {
                        sb.AppendLine($"### {key}");
                        sb.AppendLine($"Source: {f.RelativePath}");
                    }

                    sb.AppendLine("```");
                    sb.AppendLine(f.Preview);
                    sb.AppendLine("```");
                }

                sb.AppendLine();
            }
            if (request.Attachments.Any()) { sb.AppendLine("## Attachments"); foreach (var a in request.Attachments) sb.AppendLine($"- {a.Kind}: {a.Path}"); sb.AppendLine(); }
            if (request.Options.Mode == CodexRunMode.Plan) sb.AppendLine("Return an actionable implementation plan first. Do not edit files unless explicitly asked to build/implement.");
            else if (request.Options.Mode == CodexRunMode.Build) sb.AppendLine("Implement the requested changes. Keep edits scoped, run relevant checks, and report changed files.");
            sb.AppendLine("## User request"); sb.AppendLine(request.Prompt); return sb.ToString();
        }
    }
}
