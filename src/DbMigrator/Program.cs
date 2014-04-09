using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DbMigrator
{
	class Program
	{
		static int Main(string[] args)
		{
			var commands = new List<Command>
				                 {
										  new InitializeCommand(), 
										  new InsertCommand(),
										  new HistoryCommand(),
										  new ExecuteCommand(),
										  new ExtractCommand(),
										  new SetSysVersionCommand(),
										  new ExecuteSpCommand()
				                 };

			var commandName = args.FirstOrDefault() ?? ".";
			var command = commands.FirstOrDefault(x => x.GetType().Name.StartsWith(commandName, StringComparison.CurrentCultureIgnoreCase));

			var helpKeys = new[] { "-", "/" }.SelectMany(x => new[] { "?", "h", "help" }.Select(y => x + y));
			if (!args.Any() || args.Any(x => helpKeys.Contains(x.ToLower())))
			{
				if (command != null)
					Help(command);
				else
					Help(commands);
				return 0;
			}

			if (command == null)
			{
				Help(commands);
				return -1;
			}

			command.Execute(args.Skip(1).ToArray());
			return 0;
		}

		private static void Help(Command command)
		{
			var exe = Path.GetFileName(Assembly.GetEntryAssembly().Location);
			Console.WriteLine(exe);
			Console.WriteLine();
			Console.WriteLine(command.GetHelp());
			Console.WriteLine();
		}

		private static void Help(IEnumerable<Command> commands)
		{
			var exe = Path.GetFileName(Assembly.GetEntryAssembly().Location);
			Console.WriteLine(exe);
			Console.WriteLine();

			foreach (var command in commands)
				Console.WriteLine("  " + command.GetType().Name.Replace("Command", "").ToLower());
			Console.WriteLine();
		}
	}
}
