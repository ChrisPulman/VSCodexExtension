// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VSCodex.Models;

/// <summary>A primitive form field with optional choices and JSON Schema constraints.</summary>
public sealed class ElicitationField
{
    /// <summary>Protocol or schema value enum.</summary>
    private const string EnumValue = "enum";

    /// <summary>Protocol or schema value type.</summary>
    private const string TypeValue = "type";

    /// <summary>Protocol or schema value boolean.</summary>
    private const string BooleanValue = "boolean";

    /// <summary>Protocol or schema value format.</summary>
    private const string FormatValue = "format";

    /// <summary>Protocol or schema value minimum.</summary>
    private const string MinimumValue = "minimum";

    /// <summary>Protocol or schema value maximum.</summary>
    private const string MaximumValue = "maximum";

    /// <summary>Protocol or schema value exclusiveMinimum.</summary>
    private const string ExclusiveMinimumValue = "exclusiveMinimum";

    /// <summary>Protocol or schema value exclusiveMaximum.</summary>
    private const string ExclusiveMaximumValue = "exclusiveMaximum";

    /// <summary>Protocol or schema value multipleOf.</summary>
    private const string MultipleOfValue = "multipleOf";

    /// <summary>Protocol or schema value string.</summary>
    private const string StringValue = "string";

    /// <summary>Protocol or schema value number.</summary>
    private const string NumberValue = "number";

    /// <summary>Protocol or schema value integer.</summary>
    private const string IntegerValue = "integer";

    /// <summary>Bounds server supplied pattern execution.</summary>
    private const int PatternTimeoutMilliseconds = 250;

    /// <summary>Stores the field validation schema.</summary>
    private readonly JObject _schema;

    /// <summary>Initializes a new instance of the <see cref="ElicitationField"/> class.</summary>
    /// <param name="name">The name value.</param>
    /// <param name="schema">The schema value.</param>
    /// <param name="required">The required value.</param>
    public ElicitationField(string name, JObject schema, bool required)
    {
        Name = name;
        _schema = schema;
        Required = required;
        Label = (schema.Value<string>("title") ?? name) + (required ? " *" : " (optional)");
        Description = schema.Value<string>("description") ?? string.Empty;
        Choices = new();
        if (schema[EnumValue] is JArray values)
        {
            Choices.AddRange(values.Select(DisplayValue));
        }
        else if (schema.Value<string>(TypeValue) == BooleanValue)
        {
            Choices.AddRange(["true", "false"]);
        }

        if (!required && Choices.Count > 0)
        {
            Choices.Insert(0, string.Empty);
        }

        Value = schema["default"] is JToken defaultValue ? DisplayValue(defaultValue) : string.Empty;
    }

    /// <summary>Gets the JSON property name.</summary>
    public string Name { get; }

    /// <summary>Gets the field label.</summary>
    public string Label { get; }

    /// <summary>Gets the help text.</summary>
    public string Description { get; }

    /// <summary>Gets whether the server requires this field.</summary>
    public bool Required { get; }

    /// <summary>Gets available choices; an empty list allows free text.</summary>
    public List<string> Choices { get; }

    /// <summary>Gets or sets the entered text.</summary>
    public string Value { get; set; }

    /// <summary>Gets whether an empty optional string can be sent explicitly.</summary>
    public bool CanIncludeEmptyValue => !Required && _schema.Value<string>(TypeValue) == StringValue;

    /// <summary>Gets or sets whether an empty optional string is included instead of omitted.</summary>
    public bool IncludeEmptyValue { get; set; }

    /// <summary>Checks whether this editor understands every supplied constraint.</summary>
    /// <param name="schema">The schema value.</param>
    /// <returns>The operation result.</returns>
    public static bool IsSupported(JObject schema)
    {
        string? type = schema.Value<string>(TypeValue);
        var allowed = new[]
        {
            TypeValue, "title", "description", "default", EnumValue, "enumNames", "minLength", "maxLength", "pattern", FormatValue,
            MinimumValue, MaximumValue, ExclusiveMinimumValue, ExclusiveMaximumValue, MultipleOfValue
        };
        return new[] { StringValue, NumberValue, IntegerValue, BooleanValue }.Contains(type)
            && schema.Properties().All(p => allowed.Contains(p.Name))
            && HasSupportedEnum(schema, type)
            && HasSupportedFormat(schema);
    }

    /// <summary>Reads and validates the field value without throwing on user input.</summary>
    /// <param name="value">The value.</param>
    /// <param name="error">The error value.</param>
    /// <returns>The operation result.</returns>
    public bool TryRead(out JToken? value, out string error)
    {
        value = null;
        error = string.Empty;
        if (Value.Length == 0 && ((!Required && !IncludeEmptyValue) || _schema.Value<string>(TypeValue) != StringValue))
        {
            if (Required)
            {
                error = $"Enter a value for {Name}.";
            }

            return !Required;
        }

        value = ParseValue();
        if (value is null)
        {
            error = $"Enter a valid {_schema.Value<string>("type")} for {Name}.";
            return false;
        }

        try
        {
            error = ValidateValue(value);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is InvalidCastException || ex is OverflowException)
        {
            error = $"The server supplied invalid constraints for {Name}. Choose Decline or Cancel.";
        }

        return error.Length == 0;
    }

    /// <summary>Formats primitive defaults and choices consistently for the editor.</summary>
    /// <param name="value">The JSON value.</param>
    /// <returns>The editor text.</returns>
    private static string DisplayValue(JToken value) => value.Type == JTokenType.String ? value.Value<string>() ?? string.Empty : value.ToString(Formatting.None);

    /// <summary>Performs HasSupportedEnum.</summary>
    /// <param name="schema">The schema value.</param>
    /// <param name="type">The type value.</param>
    /// <returns>The operation result.</returns>
    private static bool HasSupportedEnum(JObject schema, string? type)
    {
        if (schema[EnumValue] is null)
        {
            return true;
        }

        return schema[EnumValue] is JArray choices && choices.Count > 0 && choices.All(v => MatchesType(v, type));
    }

    /// <summary>Performs HasSupportedFormat.</summary>
    /// <param name="schema">The schema value.</param>
    /// <returns>The operation result.</returns>
    private static bool HasSupportedFormat(JObject schema)
    {
        return schema[FormatValue] is null || new[] { "email", "uri", "date", "date-time" }.Contains(schema.Value<string>(FormatValue));
    }

    /// <summary>Performs MatchesType.</summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The type value.</param>
    /// <returns>The operation result.</returns>
    private static bool MatchesType(JToken value, string? type) => type switch
    {
        StringValue => value.Type == JTokenType.String,
        BooleanValue => value.Type == JTokenType.Boolean,
        IntegerValue => value.Type == JTokenType.Integer,
        _ => value.Type == JTokenType.Integer || value.Type == JTokenType.Float
    };

    /// <summary>Performs MeetsLimit.</summary>
    /// <param name="key">The key value.</param>
    /// <param name="value">The value.</param>
    /// <param name="limit">The limit value.</param>
    /// <returns>The operation result.</returns>
    private static bool MeetsLimit(string key, decimal value, decimal limit) => key switch
    {
        MinimumValue => value >= limit,
        MaximumValue => value <= limit,
        ExclusiveMinimumValue => value > limit,
        ExclusiveMaximumValue => value < limit,
        MultipleOfValue => limit > 0 && value % limit == 0,
        _ => true
    };

    /// <summary>Counts Unicode code points as required by JSON Schema string limits.</summary>
    /// <param name="text">The field value.</param>
    /// <returns>The number of Unicode code points.</returns>
    private static int CountCodePoints(string text)
    {
        int count = 0;
        for (int index = 0; index < text.Length; index++)
        {
            count++;
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                index++;
            }
        }

        return count;
    }

    /// <summary>Performs ParseValue.</summary>
    /// <returns>The operation result.</returns>
    private JValue? ParseValue() => _schema.Value<string>(TypeValue) switch
    {
        StringValue => new JValue(Value),
        BooleanValue when bool.TryParse(Value, out bool boolean) => new JValue(boolean),
        IntegerValue when long.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer) => new JValue(integer),
        NumberValue when decimal.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number) => new JValue(number),
        _ => null
    };

    /// <summary>Performs ValidateValue.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The operation result.</returns>
    private string ValidateValue(JToken value)
    {
        return _schema[EnumValue] is JArray choices && !choices.Any(v => JToken.DeepEquals(v, value))
            ? $"Choose a listed value for {Name}."
            : _schema.Value<string>(TypeValue) switch
        {
            StringValue => ValidateString(),
            NumberValue or IntegerValue => ValidateNumber(value.Value<decimal>()),
            _ => string.Empty
        };
    }

    /// <summary>Performs ValidateString.</summary>
    /// <returns>The operation result.</returns>
    private string ValidateString()
    {
        int length = CountCodePoints(Value);
        if ((_schema.Value<int?>("minLength") is int min && length < min) || (_schema.Value<int?>("maxLength") is int max && length > max))
        {
            return $"The length of {Name} does not meet the requested limits.";
        }

        string patternError = ValidatePattern();
        if (patternError.Length > 0)
        {
            return patternError;
        }

        return IsValidFormat() ? string.Empty : $"Enter a valid {_schema.Value<string>("format")} for {Name}.";
    }

    /// <summary>Performs ValidatePattern.</summary>
    /// <returns>The operation result.</returns>
    private string ValidatePattern()
    {
        try
        {
            if (_schema.Value<string>("pattern") is string pattern && !Regex.IsMatch(Value, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(PatternTimeoutMilliseconds)))
            {
                return $"{Name} does not match the requested pattern.";
            }
        }
        catch (ArgumentException)
        {
            return $"The server supplied an invalid pattern for {Name}. Choose Decline or Cancel.";
        }
        catch (RegexMatchTimeoutException)
        {
            return $"The pattern for {Name} could not be validated. Choose Decline or Cancel.";
        }

        return string.Empty;
    }

    /// <summary>Performs IsValidFormat.</summary>
    /// <returns>The operation result.</returns>
    private bool IsValidFormat() => _schema.Value<string>(FormatValue) switch
    {
        "uri" => Uri.TryCreate(Value, UriKind.Absolute, out _),
        "email" => Regex.IsMatch(Value, @"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.None, TimeSpan.FromMilliseconds(PatternTimeoutMilliseconds)),
        "date" => DateTime.TryParseExact(Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        "date-time" => Regex.IsMatch(Value, @"^\d{4}-\d{2}-\d{2}[Tt]\d{2}:\d{2}:\d{2}(\.\d+)?([Zz]|[+-]\d{2}:\d{2})$", RegexOptions.None, TimeSpan.FromMilliseconds(PatternTimeoutMilliseconds))
            && DateTimeOffset.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        _ => true
    };

    /// <summary>Performs ValidateNumber.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The operation result.</returns>
    private string ValidateNumber(decimal value)
    {
        foreach (string key in new[] { MinimumValue, MaximumValue, ExclusiveMinimumValue, ExclusiveMaximumValue, MultipleOfValue })
        {
            if (_schema.Value<decimal?>(key) is decimal limit && !MeetsLimit(key, value, limit))
            {
                return $"{Name} does not meet the requested numeric limits.";
            }
        }

        return string.Empty;
    }
}
