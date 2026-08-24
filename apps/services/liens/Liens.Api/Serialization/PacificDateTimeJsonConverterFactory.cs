using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liens.Api.Serialization;

internal sealed class PacificDateTimeJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert == typeof(DateTime) || typeToConvert == typeof(DateTime?);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(DateTime))
            return new PacificDateTimeJsonConverter();

        if (typeToConvert == typeof(DateTime?))
            return new NullablePacificDateTimeJsonConverter();

        throw new NotSupportedException($"Unsupported DateTime conversion type '{typeToConvert}'.");
    }

    private sealed class PacificDateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Expected string value for DateTime.");

            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
                throw new JsonException("Expected non-empty string value for DateTime.");

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                return parsed;

            throw new JsonException($"Invalid DateTime value '{raw}'.");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(PacificTimeHelper.Convert(value));
    }

    private sealed class NullablePacificDateTimeJsonConverter : JsonConverter<DateTime?>
    {
        private readonly PacificDateTimeJsonConverter _inner = new();

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            return _inner.Read(ref reader, typeof(DateTime), options);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(PacificTimeHelper.Convert(value.Value));
        }
    }
}
