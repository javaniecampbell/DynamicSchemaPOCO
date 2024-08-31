using System.Text.Json;
using System.Xml.Schema;
using System.Xml;
using System.Text.RegularExpressions;

namespace DynamicSchemaPOCO
{
	public static class SchemaParser
	{
		public static ISchemaElement ParseJsonSchema(string jsonSchema)
		{
			var schema = JsonSerializer.Deserialize<JsonSchema>(jsonSchema, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			return ConvertJsonSchema(schema, "Root");
		}

		private static ISchemaElement ConvertJsonSchema(JsonSchema schema, string name)
		{
			var element = new JsonSchemaElement
			{
				Name = ToPascalCase(name),
				Type = schema.Type,
				Properties = new Dictionary<string, ISchemaElement>()
			};

			if (schema.Properties != null)
			{
				foreach (var prop in schema.Properties)
				{
					string pascalCaseName = ToPascalCase(prop.Key);
					element.Properties[pascalCaseName] = ConvertJsonSchema(prop.Value, pascalCaseName);
				}
			}

			return element;
		}

		public static ISchemaElement ParseXsdSchema(string xsdContent)
		{
			XmlSchemaSet schemaSet = new XmlSchemaSet();
			using (XmlReader reader = XmlReader.Create(new System.IO.StringReader(xsdContent)))
			{
				schemaSet.Add(null, reader);
			}

			schemaSet.Compile();

			XmlSchema schema = schemaSet.Schemas().Cast<XmlSchema>().First();
			XmlSchemaElement rootElement = schema.Elements.Values.Cast<XmlSchemaElement>().First();

			return ParseSchemaElement(rootElement, "Root");
		}

		private static XsdSchemaElement ParseSchemaElement(XmlSchemaElement element, string parentName)
		{
			string pascalCaseName = ToPascalCase(element.Name);
			XsdSchemaElement schemaElement = new XsdSchemaElement
			{
				Name = pascalCaseName,
				Type = GetElementTypeName(element, $"{parentName}_{pascalCaseName}"),
				Properties = new Dictionary<string, ISchemaElement>()
			};

			XmlSchemaComplexType complexType = element.SchemaType as XmlSchemaComplexType;

			if (complexType != null)
			{
				if (complexType.Particle is XmlSchemaSequence sequence)
				{
					foreach (XmlSchemaElement childElement in sequence.Items.OfType<XmlSchemaElement>())
					{
						string childPascalCaseName = ToPascalCase(childElement.Name);
						schemaElement.Properties[childPascalCaseName] = ParseSchemaElement(childElement, schemaElement.Type);
					}
				}

				foreach (XmlSchemaAttribute attribute in complexType.Attributes)
				{
					string attributePascalCaseName = ToPascalCase(attribute.Name);
					schemaElement.Properties[attributePascalCaseName] = new XsdSchemaElement
					{
						Name = attributePascalCaseName,
						Type = GetAttributeTypeName(attribute),
						IsAttribute = true
					};
				}
			}

			return schemaElement;
		}

		private static string GetElementTypeName(XmlSchemaElement element, string elementName)
		{
			if (element.ElementSchemaType is XmlSchemaSimpleType simpleType)
			{
				return GetSimpleTypeName(simpleType);
			}
			else if (element.ElementSchemaType is XmlSchemaComplexType)
			{
				return $"Complex_{elementName}";
			}
			else if (!string.IsNullOrEmpty(element.SchemaTypeName.Name))
			{
				return MapBuiltInTypeName(element.SchemaTypeName.Name);
			}

			// If we can't determine the type, default to string
			return "string";
		}

		private static string GetAttributeTypeName(XmlSchemaAttribute attribute)
		{
			if (attribute.AttributeSchemaType != null)
			{
				return GetSimpleTypeName(attribute.AttributeSchemaType);
			}
			else if (!string.IsNullOrEmpty(attribute.SchemaTypeName.Name))
			{
				return MapBuiltInTypeName(attribute.SchemaTypeName.Name);
			}

			// If we can't determine the type, default to string
			return "string";
		}

		private static string GetSimpleTypeName(XmlSchemaSimpleType simpleType)
		{
			if (simpleType == null)
			{
				// If schemaType is null, return a default type
				return "string";
			}


			switch (simpleType.TypeCode)
			{
				case XmlTypeCode.String:
					return "string";
				case XmlTypeCode.Boolean:
					return "boolean";
				case XmlTypeCode.Int:
				case XmlTypeCode.Integer:
				case XmlTypeCode.PositiveInteger:
				case XmlTypeCode.NegativeInteger:
				case XmlTypeCode.NonNegativeInteger:
				case XmlTypeCode.NonPositiveInteger:
					return "integer";
				case XmlTypeCode.Long:
				case XmlTypeCode.UnsignedLong:
					return "long";
				case XmlTypeCode.Short:
				case XmlTypeCode.UnsignedShort:
					return "short";
				case XmlTypeCode.Float:
					return "float";
				case XmlTypeCode.Double:
					return "double";
				case XmlTypeCode.Decimal:
					return "decimal";
				case XmlTypeCode.DateTime:
					return "datetime";
				case XmlTypeCode.Date:
					return "date";
				case XmlTypeCode.Time:
					return "time";
				default:
					return "string";
			}
		}

		private static string MapBuiltInTypeName(string xsdTypeName)
		{
			return xsdTypeName.ToLower() switch
			{
				"string" => "string",
				"boolean" => "boolean",
				"decimal" => "decimal",
				"float" => "float",
				"double" => "double",
				"duration" => "timespan",
				"datetime" => "datetime",
				"time" => "time",
				"date" => "date",
				"hexbinary" => "byte[]",
				"base64binary" => "byte[]",
				"anyuri" => "uri",
				"qname" => "xmlqualifiedname",
				"notation" => "string",
				"normalizedstring" => "string",
				"token" => "string",
				"language" => "string",
				"nmtoken" => "string",
				"nmtokens" => "string[]",
				"name" => "string",
				"ncname" => "string",
				"id" => "string",
				"idref" => "string",
				"idrefs" => "string[]",
				"entity" => "string",
				"entities" => "string[]",
				"integer" => "integer",
				"nonpositiveinteger" => "integer",
				"negativeinteger" => "integer",
				"long" => "long",
				"int" => "integer",
				"short" => "short",
				"byte" => "byte",
				"nonnegativeinteger" => "integer",
				"unsignedlong" => "ulong",
				"unsignedint" => "uint",
				"unsignedshort" => "ushort",
				"unsignedbyte" => "byte",
				"positiveinteger" => "integer",
				_ => "string"
			};
		}

		private static string ToPascalCase(string input)
		{
			if (string.IsNullOrEmpty(input))
				return input;

			// Split the input string into words
			string[] words = Regex.Split(input, @"[\W_]+")
								  .Where(w => !string.IsNullOrEmpty(w))
								  .ToArray();

			// Capitalize the first letter of each word and join them
			string pascalCase = string.Join("", words.Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower()));

			// Ensure the first character is uppercase
			return char.ToUpper(pascalCase[0]) + pascalCase.Substring(1);
		}
	}
}