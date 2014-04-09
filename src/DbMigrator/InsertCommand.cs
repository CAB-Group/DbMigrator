using System;
using System.Collections.Generic;
using System.Linq;

namespace DbMigrator
{
	class InsertCommand : Command
	{
		protected override IEnumerable<Option> GetOptions()
		{
			return new List<Option>(base.GetOptions())
				       {
					       new IndexedOption("version") {IsRequired = true},
					       new NamedOption("conflict") {Converter = new EnumConverter<ConflictStrategy>()}
				       };
		}

		protected override void Execute(Args args)
		{
			var entries = ScriptLogEntry.Load(Database);
			var version = VersionNumber.Parse(args.GetValue("version"));

			if (entries.Any(x => x.Version == version.ToString() && !x.ScriptNumber.HasValue))
			{
				switch (args.GetValueOrDefault("conflict", ConflictStrategy.Fail))
				{
					case ConflictStrategy.Fail:
						throw new ApplicationException("Version already exists.");
					case ConflictStrategy.Notify:
						using (TemporaryConsoleColorWarning())
							Console.WriteLine("Version already exists.");
						return;
					case ConflictStrategy.Ignore:
						return;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			const string sql = "insert [dbo].[ScriptLog] ([Version], [User], [Date]) values (@version, @user, @date)";
			var entry = new
								{
									Date = DateTimeOffset.UtcNow,
									User = Environment.UserName,
									Version = version.ToString()
								};
			Database.Execute(sql, entry);
		}
	}
}