using System.Dynamic;
using System.Text.Json;

namespace DynamicSchemaPOCO
{
	public class DynamicSchemaObject : DynamicObject
	{
		private readonly Dictionary<string, object> _properties = new Dictionary<string, object>();

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			return _properties.TryGetValue(binder.Name, out result);
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			_properties[binder.Name] = value;
			return true;
		}

		public bool TrySetMember(string name, object value)
		{
			_properties[name] = value;
			return true;
		}

		public override IEnumerable<string> GetDynamicMemberNames()
		{
			return _properties.Keys;
		}

		public Dictionary<string, object> GetProperties()
		{
			return _properties;
		}

		public static dynamic CreateDynamicObject(JsonSchema schema)
		{
			Console.WriteLine("Creating dynamic object from schema...");
			var obj = new DynamicSchemaObject();
			foreach (var property in schema.Properties)
			{
				Console.WriteLine($"Processing property: {property.Key}");
				if (property.Value.Type == "object")
				{
					obj.TrySetMember(property.Key, CreateDynamicObject(property.Value));
				}
				else
				{
					obj.TrySetMember(property.Key, DynamicTypeGenerator.GetDefaultValue(property.Value.Type));
				}
			}
			return obj;
		}

		public static dynamic CreateDynamicObject(ISchemaElement schema)
		{
			Console.WriteLine("Creating dynamic object from schema...");
			var obj = new DynamicSchemaObject();
			foreach (var property in schema.Properties)
			{
				Console.WriteLine($"Processing property: {property.Key}");
				if (property.Value.Type.ToLower() == "object")
				{
					obj.TrySetMember(property.Key, CreateDynamicObject(property.Value));
				}
				else
				{
					obj.TrySetMember(property.Key, DynamicTypeGenerator.GetDefaultValue(property.Value.Type));
				}
			}
			return obj;
		}

		public static void PopulateDynamicObject(dynamic obj, JsonElement data)
		{
			Console.WriteLine("Populating dynamic object with data...");
			if (data.ValueKind != JsonValueKind.Object)
			{
				throw new ArgumentException("JsonElement must be an object", nameof(data));
			}

			foreach (var property in data.EnumerateObject())
			{
				try
				{
					Console.WriteLine($"Setting property: {property.Name}");
					object value = DynamicTypeGenerator.ConvertJsonElementToType(property.Value, DynamicTypeGenerator.GetTypeFromJsonValueKind(property.Value.ValueKind));
					Console.WriteLine($"Setting property {property.Name} with value: {value}");
					((DynamicSchemaObject)obj).TrySetMember(property.Name, value);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error setting property {property.Name}: {ex.GetType().Name} - {ex.Message}");
				}
			}
		}

		public static dynamic DeserializeNestedObject(JsonElement element)
		{
			Console.WriteLine("Deserializing nested object...");
			var nestedObj = new DynamicSchemaObject();
			PopulateDynamicObject(nestedObj, element);
			return nestedObj;
		}

	}
}