// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace VSCodex.Options;

/// <summary>Provides the options Provider implementation.</summary>
public static class OptionsProvider
{
    /// <summary>Provides the general Options implementation.</summary>
    [ComVisible(true)]
    [Guid("C7A22742-22C0-423B-A0DF-9C2D2F6D7DB1")]
    public sealed class GeneralOptions : DialogPage
    {
        /// <summary>Stores the model.</summary>
        private readonly VSCodexOptionsModel _model = new();

        /// <summary>Gets the automation Object.</summary>
        public override object AutomationObject => _model;

        /// <summary>Loads settings From Storage.</summary>
        public override void LoadSettingsFromStorage()
        {
            _model.LoadFromSettingsStore();
        }

        /// <summary>Saves settings To Storage.</summary>
        public override void SaveSettingsToStorage()
        {
            _model.SaveToSettingsStore();
        }
    }
}
