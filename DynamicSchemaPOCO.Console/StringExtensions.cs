using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DynamicSchemaPOCO.Extensions
{
	public static class StringExtensions
	{
		public static string ToPascalCase(this string input)
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
