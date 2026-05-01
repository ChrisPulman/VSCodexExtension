using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VSCodexExtension.Infrastructure;
using VSCodexExtension.Models;
namespace VSCodexExtension.Services
{
    public interface ISettingsStore { IObservable<ExtensionSettings> SettingsChanged { get; } ExtensionSettings Current { get; } void Save(ExtensionSettings settings); }
    public sealed class SettingsStore : ISettingsStore
    {
        private readonly JsonFileStore _store = new JsonFileStore();
        private readonly BehaviorSubject<ExtensionSettings> _settings;
        public SettingsStore()
        {
            var settings = _store.ReadOrCreate<ExtensionSettings>(LocalPaths.SettingsFile);
            Normalize(settings);
            if (settings.SkillRoots.Count == 0) settings.SkillRoots.Add(LocalPaths.UserSkillsRoot);
            _store.Write(LocalPaths.SettingsFile, settings);
            _settings = new BehaviorSubject<ExtensionSettings>(settings);
        }
        public IObservable<ExtensionSettings> SettingsChanged => _settings.AsObservable();
        public ExtensionSettings Current => _settings.Value;
        public void Save(ExtensionSettings settings) { _store.Write(LocalPaths.SettingsFile, settings); _settings.OnNext(settings); }

        private static void Normalize(ExtensionSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.DefaultModel) || settings.DefaultModel.Equals("gpt-5.4-codex", StringComparison.OrdinalIgnoreCase))
            {
                settings.DefaultModel = "gpt-5.5";
            }

            if (string.IsNullOrWhiteSpace(settings.DefaultFailoverModel))
            {
                settings.DefaultFailoverModel = "gpt-5.3-codex";
            }

            if (string.IsNullOrWhiteSpace(settings.DefaultOrchestrationModel) || settings.DefaultOrchestrationModel.Equals("gpt-5.4-codex", StringComparison.OrdinalIgnoreCase))
            {
                settings.DefaultOrchestrationModel = settings.DefaultModel;
            }

            if (string.IsNullOrWhiteSpace(settings.DefaultBudgetModel) || settings.DefaultBudgetModel.Equals("gpt-5.1-codex", StringComparison.OrdinalIgnoreCase))
            {
                settings.DefaultBudgetModel = "gpt-5.4-mini";
            }

            var defaults = new[] { "gpt-5.5", "gpt-5.4", "gpt-5.4-mini", "gpt-5.3-codex", "gpt-5.2-codex", "gpt-5.1-codex", "gpt-5-codex" };
            settings.CustomModels = (settings.CustomModels ?? new List<string>())
                .Concat(defaults)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
