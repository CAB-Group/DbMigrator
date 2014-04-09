using System;
using System.Collections.Generic;
using System.Linq;

namespace DbMigrator
{
	class ExecuteSpCommand : Command
	{
		protected override IEnumerable<Option> GetOptions()
		{
			return new List<Option>(base.GetOptions())
				       {
					       new NamedOption("storedprocedure") { IsRequired = true, Shortcut = "sp" }					       					       
				       };
		}

		protected override void Execute(Args args)
		{
			var runAtDeployScript = args.GetValue("storedprocedure");
			Console.WriteLine();
			Console.WriteLine("Server   " + args.GetValue("server"));
			Console.WriteLine("Database " + args.GetValue("database"));
			Console.WriteLine("Stored Procedure " + runAtDeployScript);

			var isInitialized = "SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[" + runAtDeployScript + "]') AND type in (N'P', N'PC')";
			if (!Database.Query(isInitialized).Any()) throw new ApplicationException("Stored procedure " + runAtDeployScript + " is missing in database.");

			Database.Execute(x => x.Execute("exec " + runAtDeployScript));
		}
	}
}


