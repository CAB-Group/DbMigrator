using System;
using System.Linq;

namespace DbMigrator
{
	class VersionNumber : IComparable<VersionNumber>, IEquatable<VersionNumber>
	{
		public const int Numbers = 3;

		private readonly string _version;

		public VersionNumber()
		{
			_version = string.Join(".", Enumerable.Repeat("0", Numbers));
		}

		private VersionNumber(string version)
		{
			_version = version;
		}

		public static VersionNumber Parse(string input)
		{
			var parts = input.Split('.').ToList();

			if (parts.Count == 0 || parts.Count > Numbers)
				throw new FormatException(input);

			for (var i = 0; i < parts.Count; i++)
			{
				int result;
				if (!int.TryParse(parts[i], out result))
					throw new FormatException(input);
			}

			return new VersionNumber(string.Join(".", parts.Concat(Enumerable.Repeat("0", Numbers - parts.Count))));
		}

		public static bool operator <(VersionNumber x, VersionNumber y)
		{
			return x.CompareTo(y) < 0;
		}

		public static bool operator >(VersionNumber x, VersionNumber y)
		{
			return x.CompareTo(y) > 0;
		}

		public override string ToString()
		{
			return _version;
		}

		public int CompareTo(VersionNumber other)
		{
			var parts = _version.Split('.');
			var otherParts = other._version.Split('.');
			for (var i = 0; i < Numbers; i++)
			{
				var result = int.Parse(parts[i]).CompareTo(int.Parse(otherParts[i]));
				if (result != 0) return result;
			}
			return 0;
		}

		public bool Equals(VersionNumber other)
		{
			return _version == other._version;
		}
	}
}