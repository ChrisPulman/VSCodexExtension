// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Commands;

/// <summary>Provides the codex Command Ids implementation.</summary>
internal static class CodexCommandIds
{
    /// <summary>Defines the command Set Guid String.</summary>
    internal const string CommandSetGuidString = "a17f6b52-76e1-48cb-855e-30c86c46a74d";

    /// <summary>Defines the open Tool Window Command Id.</summary>
    internal const int OpenToolWindowCommandId = 0x0100;

    /// <summary>Defines the open Options Command Id.</summary>
    internal const int OpenOptionsCommandId = 0x0101;

    /// <summary>Defines the create Test From Selection Command Id.</summary>
    internal const int CreateTestFromSelectionCommandId = 0x0102;

    /// <summary>Defines the debug With Codex Command Id.</summary>
    internal const int DebugWithCodexCommandId = 0x0103;

    /// <summary>Defines the create Plan Command Id.</summary>
    internal const int CreatePlanCommandId = 0x0104;

    /// <summary>Defines the ask Codex Command Id.</summary>
    internal const int AskCodexCommandId = 0x0105;

    /// <summary>Defines the explain Selection Command Id.</summary>
    internal const int ExplainSelectionCommandId = 0x0106;

    /// <summary>Defines the fix Selection Command Id.</summary>
    internal const int FixSelectionCommandId = 0x0107;

    /// <summary>Defines the review Selection Command Id.</summary>
    internal const int ReviewSelectionCommandId = 0x0108;

    /// <summary>Defines the optimize Selection Command Id.</summary>
    internal const int OptimizeSelectionCommandId = 0x0109;

    /// <summary>Defines the generate Docs Command Id.</summary>
    internal const int GenerateDocsCommandId = 0x010A;

    /// <summary>Defines the configure Memory Command Id.</summary>
    internal const int ConfigureMemoryCommandId = 0x010B;

    /// <summary>Defines the fix Active Exception Command Id.</summary>
    internal const int FixActiveExceptionCommandId = 0x010C;

    /// <summary>Defines the fix Active Error Command Id.</summary>
    internal const int FixActiveErrorCommandId = 0x010D;

    /// <summary>Defines the fix Test Failure Command Id.</summary>
    internal const int FixTestFailureCommandId = 0x010E;

    /// <summary>Defines the codex Tools Menu Group.</summary>
    internal const int CodexToolsMenuGroup = 0x1020;

    /// <summary>Defines the codex Editor Context Menu Group.</summary>
    internal const int CodexEditorContextMenuGroup = 0x1021;

    /// <summary>Defines the codex View Menu Group.</summary>
    internal const int CodexViewMenuGroup = 0x1022;

    /// <summary>Defines the codex Project Context Menu Group.</summary>
    internal const int CodexProjectContextMenuGroup = 0x1023;

    /// <summary>Defines the codex Solution Context Menu Group.</summary>
    internal const int CodexSolutionContextMenuGroup = 0x1024;

    /// <summary>Defines the codex Debug Menu Group.</summary>
    internal const int CodexDebugMenuGroup = 0x1025;
}
