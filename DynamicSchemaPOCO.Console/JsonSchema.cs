namespace DynamicSchemaPOCO
{
	public class JsonSchema
	{
		public string Type { get; set; }
		public Dictionary<string, JsonSchema> Properties { get; set; }
	}
}