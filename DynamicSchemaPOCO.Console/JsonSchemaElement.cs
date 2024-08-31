namespace DynamicSchemaPOCO
{
	public class JsonSchemaElement : ISchemaElement
	{
		public string Name { get; set; }
		public string Type { get; set; }
		public Dictionary<string, ISchemaElement> Properties { get; set; } = new Dictionary<string, ISchemaElement>();
	}
}