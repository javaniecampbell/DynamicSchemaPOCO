using System.Reflection.Emit;
using System.Reflection;
using System.Collections;
using System.Xml.Linq;
using DynamicSchemaPOCO.Extensions;
using System.Text.Json;

namespace DynamicSchemaPOCO
{
	public static class DynamicTypeGenerator
	{
		public static Type GenerateType(JsonSchema schema, string typeName, ModuleBuilder moduleBuilder = null)
		{
			if (moduleBuilder == null)
			{
				AssemblyName assemblyName = new AssemblyName("DynamicAssembly");
				AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
				moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
			}

			TypeBuilder typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);

			foreach (var property in schema.Properties)
			{
				Type propertyType;
				if (property.Value.Type == "object" && property.Value.Properties != null)
				{
					propertyType = GenerateType(property.Value, $"{typeName}_{property.Key}", moduleBuilder);
				}
				else
				{
					propertyType = GetTypeFromSchema(property.Value);
				}

				CreateProperty(typeBuilder, property.Key, propertyType);
			}

			return typeBuilder.CreateType();
		}

		public static Type GenerateType(ISchemaElement schema, string typeName, ModuleBuilder moduleBuilder = null)
		{
			if (moduleBuilder == null)
			{
				AssemblyName assemblyName = new AssemblyName("DynamicAssembly");
				AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
				moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
			}

			TypeBuilder typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);

			foreach (var property in schema.Properties)
			{
				Type propertyType = GetTypeFromSchemaElement(property.Value, moduleBuilder);
				CreateProperty(typeBuilder, property.Key, propertyType);
			}

			return typeBuilder.CreateType();
		}

		private static Type GetTypeFromSchemaElement(ISchemaElement element, ModuleBuilder moduleBuilder)
		{
			if (element.Type.StartsWith("Complex_") || element.Properties.Count > 0)
			{
				return GenerateType(element, element.Type, moduleBuilder);
			}

			return element.Type.ToLower() switch
			{
				"string" => typeof(string),
				"integer" => typeof(int),
				"boolean" => typeof(bool),
				"decimal" => typeof(decimal),
				"double" => typeof(double),
				"datetime" => typeof(DateTime),
				_ => typeof(object)
			};
		}

		private static void CreateProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
		{
			FieldBuilder fieldBuilder = typeBuilder.DefineField($"_{propertyName}", propertyType, FieldAttributes.Private);
			PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);

			MethodBuilder getMethodBuilder = typeBuilder.DefineMethod($"get_{propertyName}",
				MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
				propertyType,
				Type.EmptyTypes);

			ILGenerator getIL = getMethodBuilder.GetILGenerator();
			getIL.Emit(OpCodes.Ldarg_0);
			getIL.Emit(OpCodes.Ldfld, fieldBuilder);
			getIL.Emit(OpCodes.Ret);

			MethodBuilder setMethodBuilder = typeBuilder.DefineMethod($"set_{propertyName}",
				MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
				null,
				new Type[] { propertyType });

			ILGenerator setIL = setMethodBuilder.GetILGenerator();
			setIL.Emit(OpCodes.Ldarg_0);
			setIL.Emit(OpCodes.Ldarg_1);
			setIL.Emit(OpCodes.Stfld, fieldBuilder);
			setIL.Emit(OpCodes.Ret);

			propertyBuilder.SetGetMethod(getMethodBuilder);
			propertyBuilder.SetSetMethod(setMethodBuilder);
		}

		public static object CreateInstance(Type type, DynamicSchemaObject dynamicObject)
		{
			object instance = Activator.CreateInstance(type);

			foreach (var property in dynamicObject.GetProperties())
			{
				string pascalCasePropertyName = property.Key.ToPascalCase();
				PropertyInfo prop = type.GetProperty(pascalCasePropertyName);
				if (prop != null && prop.CanWrite)
				{
					object convertedValue = ConvertValue(property.Value, prop.PropertyType);
					prop.SetValue(instance, convertedValue);
				}
			}

			return instance;
		}

		public static object CreateInstanceFromXml(Type type, XElement xmlElement)
		{
			object instance = Activator.CreateInstance(type);

			foreach (var property in type.GetProperties())
			{
				// Handle attributes
				string xmlElementPascalCaseName = property.Name.ToPascalCase();
				XAttribute attribute = xmlElement.Attribute(xmlElementPascalCaseName.ToLower());
				if (attribute != null)
				{
					property.SetValue(instance, ConvertValueToType(attribute.Value, property.PropertyType));
					continue;
				}

				// Handle elements
				XElement childElement = xmlElement.Element(xmlElementPascalCaseName.ToLower());
				if (childElement != null)
				{
					if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
					{
						// Recursive call for complex types
						property.SetValue(instance, CreateInstanceFromXml(property.PropertyType, childElement));
					}
					else
					{
						property.SetValue(instance, ConvertXElementToType(childElement, property.PropertyType));
					}
				}
			}

			return instance;
		}

		private static object ConvertValue(object value, Type targetType)
		{
			if (value == null)
			{
				return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
			}

			Type valueType = value.GetType();

			// If the value is already of the target type, return it
			if (targetType.IsAssignableFrom(valueType))
			{
				return value;
			}

			// Handle nullable types
			if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				targetType = Nullable.GetUnderlyingType(targetType);
			}

			// Numeric conversions
			if (IsNumericType(targetType) && IsNumericType(valueType))
			{
				return Convert.ChangeType(value, targetType);
			}

			// String to other type conversions
			if (value is string stringValue)
			{
				if (targetType == typeof(Guid))
					return Guid.Parse(stringValue);

				if (targetType == typeof(DateTime))
					return DateTime.Parse(stringValue);

				if (targetType == typeof(TimeSpan))
					return TimeSpan.Parse(stringValue);

				if (targetType.IsEnum)
					return Enum.Parse(targetType, stringValue);
			}

			// Other type to string conversion
			if (targetType == typeof(string))
			{
				return value.ToString();
			}

			// Collection conversions
			if (typeof(IEnumerable).IsAssignableFrom(targetType) && typeof(IEnumerable).IsAssignableFrom(valueType))
			{
				return ConvertCollection(value, targetType);
			}

			// Complex type conversion (assuming nested DynamicSchemaObject)
			if (value is DynamicSchemaObject dynamicObject)
			{
				return CreateInstance(targetType, dynamicObject);
			}

			// If we can't convert, throw a more informative exception
			//throw new InvalidCastException($"Cannot convert from {valueType} to {targetType}");
			return value;
		}

		private static bool IsNumericType(Type type)
		{
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Byte:
				case TypeCode.SByte:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
				case TypeCode.UInt64:
				case TypeCode.Int16:
				case TypeCode.Int32:
				case TypeCode.Int64:
				case TypeCode.Decimal:
				case TypeCode.Double:
				case TypeCode.Single:
					return true;
				default:
					return false;
			}
		}

		private static object ConvertCollection(object value, Type targetType)
		{
			var sourceCollection = ((IEnumerable)value).Cast<object>();
			Type elementType = targetType.IsArray ? targetType.GetElementType() : targetType.GenericTypeArguments[0];
			var convertedList = sourceCollection.Select(item => ConvertValue(item, elementType)).ToList();

			if (targetType.IsArray)
			{
				Array array = Array.CreateInstance(elementType, convertedList.Count);
				for (int i = 0; i < convertedList.Count; i++)
				{
					array.SetValue(convertedList[i], i);
				}
				return array;
			}
			else if (typeof(List<>).MakeGenericType(elementType).IsAssignableFrom(targetType))
			{
				return convertedList;
			}
			else
			{
				var instance = Activator.CreateInstance(targetType);
				var addMethod = targetType.GetMethod("Add");
				foreach (var item in convertedList)
				{
					addMethod.Invoke(instance, new[] { item });
				}
				return instance;
			}
		}

		private static Type GetTypeFromSchema(JsonSchema schema)
		{
			return schema.Type switch
			{
				"string" => typeof(string),
				"integer" => typeof(int),
				"number" => typeof(double),
				"boolean" => typeof(bool),
				"object" => typeof(object),
				"array" => typeof(List<object>),
				_ => typeof(object)
			};
		}

		private static object ConvertXElementToType(XElement element, Type targetType)
		{
			if (string.IsNullOrEmpty(element.Value))
				return null;

			if (targetType == typeof(string))
				return element.Value;

			if (targetType == typeof(int))
				return int.Parse(element.Value);

			if (targetType == typeof(bool))
				return bool.Parse(element.Value);

			if (targetType == typeof(decimal))
				return decimal.Parse(element.Value);

			if (targetType == typeof(double))
				return double.Parse(element.Value);

			if (targetType == typeof(DateTime))
				return DateTime.Parse(element.Value);

			if (targetType.IsClass && targetType != typeof(string))
				return CreateInstanceFromXml(targetType, element);

			return element.Value;
		}

		private static object ConvertValueToType(string value, Type targetType)
		{
			if (string.IsNullOrEmpty(value))
				return null;

			if (targetType == typeof(string))
				return value;

			if (targetType == typeof(int))
				return int.Parse(value);

			if (targetType == typeof(bool))
				return bool.Parse(value);

			if (targetType == typeof(decimal))
				return decimal.Parse(value);

			if (targetType == typeof(double))
				return double.Parse(value);

			if (targetType == typeof(DateTime))
				return DateTime.Parse(value);

			return value;
		}

		public static object ConvertJsonElementToType(JsonElement element, Type targetType)
		{
			Console.WriteLine($"Converting JsonElement to type: {targetType}");
			return element.ValueKind switch
			{
				JsonValueKind.String => element.GetString(),
				JsonValueKind.Number when targetType == typeof(int) => element.GetInt32(),
				JsonValueKind.Number => element.GetDouble(),
				JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
				JsonValueKind.Object => DynamicSchemaObject.DeserializeNestedObject(element),
				JsonValueKind.Array => element.Deserialize<List<object>>(),
				_ => null
			};
		}

		public static Type GetTypeFromJsonValueKind(JsonValueKind valueKind)
		{
			return valueKind switch
			{
				JsonValueKind.String => typeof(string),
				JsonValueKind.Number => typeof(double),
				JsonValueKind.True or JsonValueKind.False => typeof(bool),
				JsonValueKind.Object => typeof(DynamicSchemaObject),
				JsonValueKind.Array => typeof(List<object>),
				_ => typeof(object)
			};
		}

		public static object GetDefaultValue(string type)
		{
			return type switch
			{
				"string" => "",
				"integer" => 0,
				"number" => 0.0,
				"boolean" => false,
				"array" => new List<object>(),
				_ => null
			};
		}

	}
}