namespace DynamicSchemaPOCO
{
	public interface ISchemaElement
	{
		string Name { get; set; }
		string Type { get; set; }
		Dictionary<string, ISchemaElement> Properties { get; set; }
	}
}