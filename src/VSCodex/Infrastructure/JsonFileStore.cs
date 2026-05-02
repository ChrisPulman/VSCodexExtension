using System.IO;
using Newtonsoft.Json;

namespace VSCodex.Infrastructure;

public sealed class JsonFileStore
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { Formatting = Formatting.Indented, TypeNameHandling = TypeNameHandling.None, NullValueHandling = NullValueHandling.Ignore };
    public T ReadOrCreate<T>(string path) where T : new()
    {
        if (!File.Exists(path)) { var created = new T(); Write(path, created); return created; }
        return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), Settings) ?? new T();
    }
    public void Write<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path); if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonConvert.SerializeObject(value, Settings));
    }
}
