using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaceDay.Api.Common;

/// <summary>Accepts "HH:mm" and "HH:mm:ss" (as in the Part 1 plan) and writes "HH:mm:ss".</summary>
public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            return value;
        throw new JsonException("Time must be in the format HH:mm or HH:mm:ss.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
}
