using System.ComponentModel;
using Nuke.Common.Tooling;

namespace VSCodex.Building;

[TypeConverter(typeof(TypeConverter<Configuration>))]
public sealed class Configuration : Enumeration
{
    public static readonly Configuration Debug = new() { Value = nameof(Debug) };
    public static readonly Configuration Release = new() { Value = nameof(Release) };

    public static implicit operator string(Configuration configuration) =>
        configuration?.Value ?? string.Empty;

    public override string ToString() => Value;
}
