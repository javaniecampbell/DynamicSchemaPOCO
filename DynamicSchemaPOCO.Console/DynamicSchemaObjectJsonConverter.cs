using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicSchemaPOCO
{
	public class DynamicSchemaObjectJsonConverter : JsonConverter<DynamicSchemaObject>
	{
		public override DynamicSchemaObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			throw new NotImplementedException();
		}

		public override void Write(Utf8JsonWriter writer, DynamicSchemaObject value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();
			foreach (var kvp in value.GetProperties())
			{
				writer.WritePropertyName(kvp.Key);
				JsonSerializer.Serialize(writer, kvp.Value, options);
			}
			writer.WriteEndObject();
		}
	}
}