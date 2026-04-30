#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[1]
REQUIRED = ['VSCodexExtension.sln','docs/PLAN.md','src/VSCodexExtension/VSCodexExtension.csproj','src/VSCodexExtension/source.extension.vsixmanifest','src/VSCodexExtension/VSCodexExtensionPackage.cs','src/VSCodexExtension/ToolWindows/CodexToolWindowPane.cs','src/VSCodexExtension/ViewModels/CodexToolWindowViewModel.cs','src/VSCodexExtension/Views/CodexToolWindowControl.xaml','src/VSCodexExtension/Resources/codex-bridge.mjs']
def fail(msg): print('FAIL:', msg); sys.exit(1)
for rel in REQUIRED:
    p = ROOT / rel
    if not p.exists(): fail(f'missing {rel}')
    if p.is_file() and p.stat().st_size == 0: fail(f'empty {rel}')
for rel in ['src/VSCodexExtension/VSCodexExtension.csproj','src/VSCodexExtension/source.extension.vsixmanifest','src/VSCodexExtension/Views/CodexToolWindowControl.xaml','src/VSCodexExtension/Commands/CodexCommands.vsct']:
    try: ET.parse(ROOT / rel)
    except Exception as e: fail(f'XML parse failed for {rel}: {e}')
cs_files = [p for p in (ROOT / 'src/VSCodexExtension').rglob('*.cs') if 'bin' not in p.parts and 'obj' not in p.parts]
if len(cs_files) < 12: fail(f'expected extensive C# implementation, found {len(cs_files)} files')
text = '\n'.join(p.read_text(encoding='utf-8', errors='ignore') for p in cs_files)
for token in ['ReactiveCommand','IObservable<','CodexSdkJsonClient','SkillIndexService','McpConfigService','MemoryStore','WorkspaceContextService','ReactiveUI.Extensions','ProvideOptionPage','ProvideAutoLoad','CodexOptionsPage','OpenOptionsCommandId','TaskOrchestrationService','OrchestrationRunPlan','UseMultiAgentOrchestration','SearchContextReferences','ResolveHashReferences','InputAreaHeight','AttachClipboardImage','RxAppBuilder','McpToolCatalogService','CreateTestFromSelectionCommandId','DebugWithCodexCommandId','CreatePlanCommandId','BudgetDrivenModelSelection']:
    if token not in text: fail(f'missing implementation token {token}')
print('OK: structure, XML, and key implementation tokens validated')
print(f'C# files: {len(cs_files)}')
print(f'Root: {ROOT}')
