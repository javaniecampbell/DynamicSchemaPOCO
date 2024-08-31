using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Xml.Linq;

namespace DynamicSchemaPOCO
{

	internal class Program
	{
		static void Main(string[] args)
		{
			try
			{
				Console.WriteLine("JSON Example:");
				ProcessJsonExample();

				Console.WriteLine("\nXSD Example:");
				ProcessXsdExample();

				Console.ReadKey();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error occurred: {ex.GetType().Name}");
				Console.WriteLine($"Message: {ex.Message}");
				Console.WriteLine($"Stack Trace: {ex.StackTrace}");
			}
		}

		private static void ProcessJsonExample()
		{
			string schemaJson =
							"""
                    {
                       "type": "object",
                        "properties": {
                            "name": { "type": "string" },
                            "age": { "type": "integer" },
                            "address": {
                                "type": "object",
                                "properties": {
                                    "street": { "type": "string" },
                                    "city": { "type": "string" }
                                }
                            }
                        }
                    }
                """;

			Console.WriteLine("Deserializing schema...");
			ISchemaElement schema = SchemaParser.ParseJsonSchema(schemaJson);

			string jsonData = """
                    {
                        "name": "John Doe",
                        "age": 30,
                        "address": {
                            "street": "123 Main St",
                            "city": "Anytown"
                        }
                    }
                """;

			Console.WriteLine("Creating dynamic object...");
			dynamic dynamicInstance = DynamicSchemaObject.CreateDynamicObject(schema);

			Console.WriteLine("Populating dynamic object...");
			DynamicSchemaObject.PopulateDynamicObject(dynamicInstance, JsonSerializer.Deserialize<JsonElement>(jsonData));

			Console.WriteLine("Accessing dynamic properties...");
			Console.WriteLine($"Name: {dynamicInstance.name}");
			Console.WriteLine($"Age: {dynamicInstance.age}");

			var options = new JsonSerializerOptions();
			options.Converters.Add(new DynamicSchemaObjectJsonConverter());
			Console.WriteLine($"Address: {JsonSerializer.Serialize(dynamicInstance.address, options)}");

			Console.WriteLine("Generating static type...");
			Type generatedType = DynamicTypeGenerator.GenerateType(schema, "GeneratedType");

			Console.WriteLine("Creating static type instance...");
			object staticInstance = DynamicTypeGenerator.CreateInstance(generatedType, dynamicInstance);

			Console.WriteLine("Accessing properties of static type...");
			Console.WriteLine($"Name: {generatedType.GetProperty("Name").GetValue(staticInstance)}");
			Console.WriteLine($"Age: {generatedType.GetProperty("Age").GetValue(staticInstance)}");

			var addressProp = generatedType.GetProperty("Address");
			if (addressProp != null)
			{
				var address = addressProp.GetValue(staticInstance);
				if (address != null)
				{
					Console.WriteLine($"Address Street: {address.GetType().GetProperty("Street")?.GetValue(address)}");
					Console.WriteLine($"Address City: {address.GetType().GetProperty("City")?.GetValue(address)}");
				}
				else
				{
					Console.WriteLine("Address is null");
				}
			}
			else
			{
				Console.WriteLine("Address property not found");
			}
		}

		static void ProcessXsdExample()
		{
			string xsdSchema = @"
                <xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>
                  <xs:element name='person'>
                    <xs:complexType>
                      <xs:sequence>
                        <xs:element name='name' type='xs:string'/>
                        <xs:element name='age' type='xs:integer'/>
                        <xs:element name='address'>
                          <xs:complexType>
                            <xs:sequence>
                              <xs:element name='street' type='xs:string'/>
                              <xs:element name='city' type='xs:string'/>
                            </xs:sequence>
                          </xs:complexType>
                        </xs:element>
                      </xs:sequence>
                      <xs:attribute name='id' type='xs:string'/>
                    </xs:complexType>
                  </xs:element>
                </xs:schema>";

			string xmlData = @"
                <person id='12345'>
                  <name>Jane Smith</name>
                  <age>35</age>
                  <address>
                    <street>456 Elm St</street>
                    <city>Othertown</city>
                  </address>
                </person>";

			Console.WriteLine("Parsing XSD schema...");
			ISchemaElement schema = SchemaParser.ParseXsdSchema(xsdSchema);

			Console.WriteLine("Generating static type...");
			Type generatedType = DynamicTypeGenerator.GenerateType(schema, "XsdPerson");

			Console.WriteLine("Parsing XML data...");
			XElement xmlElement = XElement.Parse(xmlData);

			Console.WriteLine("Creating static type instance...");
			object instance = DynamicTypeGenerator.CreateInstanceFromXml(generatedType, xmlElement);

			Console.WriteLine("Accessing properties of static type...");
			Utilities.PrintProperties(instance, generatedType, 0);
			Console.WriteLine($"ID: {generatedType.GetProperty("Id")?.GetValue(instance)}");
			Console.WriteLine($"Name: {generatedType.GetProperty("Name")?.GetValue(instance)}");
			Console.WriteLine($"Age: {generatedType.GetProperty("Age")?.GetValue(instance)}");

			//var addressProp = generatedType.GetProperty("address");
			//if (addressProp != null)
			//{
			//	var address = addressProp.GetValue(instance);
			//	if (address != null)
			//	{
			//		Console.WriteLine($"Street: {address.GetType().GetProperty("street")?.GetValue(address)}");
			//		Console.WriteLine($"City: {address.GetType().GetProperty("city")?.GetValue(address)}");
			//	}
			//}
		}

		
	}
}