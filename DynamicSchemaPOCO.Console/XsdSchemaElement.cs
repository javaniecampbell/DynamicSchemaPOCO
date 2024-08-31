namespace DynamicSchemaPOCO
{
	public class XsdSchemaElement : ISchemaElement
	{
		public string Name { get; set; }
		public string Type { get; set; }
		public Dictionary<string, ISchemaElement> Properties { get; set; } = new Dictionary<string, ISchemaElement>();
		public bool IsAttribute { get; set; }
	}
}