using System;
using System.Collections.Generic;
using System.Linq;

namespace DbMigrator
{
	class HistoryCommand : Command
	{

		protected override IEnumerable<Option> GetOptions()
		{
			return new List<Option>(base.GetOptions())
				       {
					       new NamedOption("count") { Shortcut = "c" }
				       };
		}

		protected override void Execute(Args args)
		{
			var count = int.Parse(args.GetValueOrDefault("count", "10"));
			var entries = ScriptLogEntry.Load(Database, count).ToList();

			Console.WriteLine();
			Console.WriteLine(ScriptLogEntry.Format(entries));
			Console.WriteLine();
		}
	}
}