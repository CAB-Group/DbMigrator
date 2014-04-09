using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DbMigrator
{
	class ScriptLogEntry
	{
		public string Version { get; set; }
		public int? ScriptNumber { get; set; }
		public DateTimeOffset Date { get; set; }
		public string User { get; set; }

		public string ScriptName
		{
			get { return (ScriptNumber.HasValue ? string.Format("{0}_{1}", Version, ScriptNumber) : Version) + ".sql"; }
		}

		public static IEnumerable<ScriptLogEntry> Load(Database database, int? count = 0)
		{
			var sql = string.Format("select top {0} * from ScriptLog order by [Date] asc", count);
			sql = sql.Replace("top 0 *", "*");
			return database.Query<ScriptLogEntry>(sql);
		}

		public static string Format(IEnumerable<ScriptLogEntry> entries)
		{
			entries = entries as ICollection<ScriptLogEntry> ?? entries.ToList();

			if (!entries.Any())
				return "No history";

			var lines = new List<string[]> { new[] { "Version", "ScriptNumber", "User", "Date" } };
			lines.AddRange(entries.Select(x => new[] { x.Version.ToString(), x.ScriptNumber.ToString(), x.User, x.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") }));
			var lengths = Enumerable.Range(0, lines[0].Length).Select(i => lines.Max(x => x[i].Length) + 2).ToArray();
			var builder = new StringBuilder();
			for (var i = 0; i < lines.Count; i++)
			{
				var line = lines[i];
				for (var j = 0; j < line.Length; j++)
					builder.Append(line[j].PadRight(lengths[j]));
				builder.AppendLine();
				if (i == 0)
					builder.AppendLine("".PadRight(lengths.Sum(), '-'));
			}
			return builder.ToString().TrimEnd();
		}

		public Version GetComparableVersion(IncludeScriptNumber option)
		{
			return ScriptNumber.HasValue && option == IncludeScriptNumber.Yes 
                ? new Version(Version + "." + ScriptNumber.Value) 
                : new Version(Version);
		}
	}
}