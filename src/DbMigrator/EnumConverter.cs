using System;
using System.Linq;

namespace DbMigrator
{
	class EnumConverter<T> : IValueConverter
	{
		public EnumConverter()
		{
			if (!typeof(T).IsEnum) throw new ArgumentException();
		}

		public string GetPatternOrNull()
		{
			var separator = IsFlagsEnum ? "|" : ",";
			return string.Join(separator, Enum.GetNames(typeof(T)));
		}

		public object Convert(string value)
		{
			return Enum.Parse(typeof(T), value.Replace('|', ','), true);
		}

		private static bool IsFlagsEnum
		{
			get { return typeof(T).GetCustomAttributes(typeof(FlagsAttribute), false).Any(); }
		}
	}
}