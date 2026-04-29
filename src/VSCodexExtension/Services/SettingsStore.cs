using System;
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
            if (settings.SkillRoots.Count == 0) settings.SkillRoots.Add(LocalPaths.UserSkillsRoot);
            _store.Write(LocalPaths.SettingsFile, settings);
            _settings = new BehaviorSubject<ExtensionSettings>(settings);
        }
        public IObservable<ExtensionSettings> SettingsChanged => _settings.AsObservable();
        public ExtensionSettings Current => _settings.Value;
        public void Save(ExtensionSettings settings) { _store.Write(LocalPaths.SettingsFile, settings); _settings.OnNext(settings); }
    }
}
