using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using VSCodexExtension.Services;
using VSCodexExtension.ViewModels;
using VSCodexExtension.Views;
namespace VSCodexExtension.ToolWindows
{
    [Guid("ee7f4f9f-8f35-46cb-9a77-a09e33f60b60")]
    public sealed class CodexToolWindowPane : ToolWindowPane
    {
        public CodexToolWindowPane() : base(null)
        {
            Caption = "Codex";
            var settings = new SettingsStore();
            var memory = new MemoryStore();
            var skills = new SkillIndexService();
            var mcp = new McpConfigService();
            var workspace = new WorkspaceContextService(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider);
            var session = new SessionStore();
            var sdk = new CodexSdkJsonClient(settings);
            var cli = new CodexCliClient(settings);
            var orchestrator = new CodexOrchestrator(sdk, cli);
            Content = new CodexToolWindowControl { DataContext = new CodexToolWindowViewModel(settings, memory, skills, mcp, workspace, session, orchestrator) };
        }
    }
}
