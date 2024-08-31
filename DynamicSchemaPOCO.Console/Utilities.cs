namespace DynamicSchemaPOCO
{
	public static class Utilities
	{
		public static void PrintProperties(object obj, Type type, int indent)
		{
			foreach (var prop in type.GetProperties())
			{
				var value = prop.GetValue(obj);
				var indentString = new string(' ', indent * 2);

				if (value == null)
				{
					Console.WriteLine($"{indentString}{prop.Name}: null");
				}
				else if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
				{
					Console.WriteLine($"{indentString}{prop.Name}:");
					PrintProperties(value, prop.PropertyType, indent + 1);
				}
				else
				{
					Console.WriteLine($"{indentString}{prop.Name}: {value}");
				}
			}
		}
	}
}