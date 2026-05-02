#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[1]
SOLUTION = 'src/VSCodexExtension.slnx'
REQUIRED = [SOLUTION,'docs/PLAN.md','scripts/install-vsix-experimental.ps1','src/VSCodexExtension/VSCodexExtension.csproj','src/VSCodexExtension/source.extension.vsixmanifest','src/VSCodexExtension/VSCodexExtensionPackage.cs','src/VSCodexExtension/ToolWindows/VSCodexToolWindowPane.cs','src/VSCodexExtension/ViewModels/VSCodexToolWindowViewModel.cs','src/VSCodexExtension/Views/VSCodexToolWindowControl.xaml','src/VSCodexExtension/Resources/codex-bridge.mjs']
def fail(msg): print('FAIL:', msg); sys.exit(1)
solution_files = sorted(str(p.relative_to(ROOT)).replace('\\', '/') for p in ROOT.rglob('*.sln*') if '.vs' not in p.parts and 'bin' not in p.parts and 'obj' not in p.parts)
if solution_files != [SOLUTION]: fail(f'expected only {SOLUTION}, found {solution_files}')
for rel in REQUIRED:
    p = ROOT / rel
    if not p.exists(): fail(f'missing {rel}')
    if p.is_file() and p.stat().st_size == 0: fail(f'empty {rel}')
for rel in [SOLUTION,'src/VSCodexExtension/VSCodexExtension.csproj','src/VSCodexExtension/source.extension.vsixmanifest','src/VSCodexExtension/Views/VSCodexToolWindowControl.xaml','src/VSCodexExtension/Commands/CodexCommands.vsct']:
    try: ET.parse(ROOT / rel)
    except Exception as e: fail(f'XML parse failed for {rel}: {e}')
cs_files = [p for p in (ROOT / 'src/VSCodexExtension').rglob('*.cs') if 'bin' not in p.parts and 'obj' not in p.parts]
if len(cs_files) < 12: fail(f'expected extensive C# implementation, found {len(cs_files)} files')
text = '\n'.join(p.read_text(encoding='utf-8', errors='ignore') for p in cs_files)
text += '\n' + (ROOT / 'src/VSCodexExtension/VSCodexExtension.csproj').read_text(encoding='utf-8', errors='ignore')
text += '\n' + (ROOT / SOLUTION).read_text(encoding='utf-8', errors='ignore')
for token in ['ReactiveCommand','IObservable<','CodexSdkJsonClient','SkillIndexService','McpConfigService','MemoryStore','WorkspaceContextService','ReactiveUI.Extensions','ProvideAutoLoad','OpenOptionsCommandId','ShowSettingsAsync','SelectedToolTabIndex','TaskOrchestrationService','OrchestrationRunPlan','UseMultiAgentOrchestration','SearchContextReferences','ResolveHashReferences','InputAreaHeight','AttachClipboardImage','RxAppBuilder','McpToolCatalogService','CreateTestFromSelectionCommandId','DebugWithCodexCommandId','CreatePlanCommandId','BudgetDrivenModelSelection','VSSDKBuildToolsAutoSetup','ProjectCapability','CreateVsixContainer','InstallVSCodexVsixForDebugging','install-vsix-experimental.ps1']:
    if token not in text: fail(f'missing implementation token {token}')
print('OK: structure, XML, and key implementation tokens validated')
print(f'C# files: {len(cs_files)}')
print(f'Root: {ROOT}')
