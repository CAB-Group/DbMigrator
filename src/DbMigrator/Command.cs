using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace DbMigrator
{
    abstract class Command
    {
        private Args _args;

        public int Execute(string[] args)
        {
            try
            {
                _args = Parse(args);
                Execute(_args);
                return 0;
            }
            catch (Exception exception)
            {
                using (TemporaryConsoleColorError())
                    Console.WriteLine(exception.Message);
                return 1;
            }
        }

        private Args Parse(IEnumerable<string> args)
        {
            var options = GetOptions();
            return new Args(args, options);
        }

        public string GetHelp()
        {
            var name = GetType().Name.Replace("Command", "").ToLower();
            var options = GetOptions().ToList();

            var builder = new StringBuilder();
            builder.AppendLine(name);
            builder.AppendLine();

            foreach (var option in options.OrderBy(x => !x.IsRequired).ThenBy(x => x.GetType().Name).ThenBy(x => x.Name))
                builder.AppendLine(GetHelp(option));

            return builder.ToString().TrimEnd();
        }

        private static string GetHelp(Option option)
        {
            var indexedOption = option as IndexedOption;
            if (indexedOption != null) return GetHelp(indexedOption);
            var namedOption = option as NamedOption;
            if (namedOption != null) return GetHelp(namedOption);
            throw new ArgumentException();
        }

        private static string GetHelp(IndexedOption option)
        {
            var content = string.Format("<{0}>", option.Name);
            content = option.IsRequired ? content : string.Format("[{0}]", content);
            if (!string.IsNullOrEmpty(option.Description))
                content += "  " + option.Description;
            return content;
        }

        private static string GetHelp(NamedOption option)
        {
            var content = string.Format("-{0}", option.Name);
            if (!option.IsFlag) content += ":<value>";
            content = option.IsRequired ? content : string.Format("[{0}]", content);

            if (!string.IsNullOrEmpty(option.Shortcut))
                content += string.Format("  (-{0})", option.Shortcut);

            if (!string.IsNullOrEmpty(option.Description))
                content += "  " + option.Description;

            var pattern = option.Converter.GetPatternOrNull();
            if (!string.IsNullOrEmpty(pattern))
                content += string.Format(" Allowed values: {0}", pattern);

            return content;
        }

        protected virtual IEnumerable<Option> GetOptions()
        {
            return new List<Option>
				       {
					       new NamedOption("server") { Shortcut = "svr", Description = "Defaults to localhost" },
							 new NamedOption("database") { IsRequired = true, Shortcut = "db" },
							 new NamedOption("user"),
							 new NamedOption("password") { Shortcut = "pwd" }
						 };
        }

        protected abstract void Execute(Args args);

        protected Database Database
        {
            get { return new Database(() => CreateConnection(_args)); }
        }

        private static SqlConnection CreateConnection(Args args)
        {
            var database = args.GetValue("database");
            var server = args.GetValueOrDefault("server", Environment.MachineName);
            var user = args.GetValueOrNull("user");
            var password = args.GetValueOrNull("password");
            var credentials = user == null
                                        ? "Integrated Security=True"
                                        : string.Format("User ID={0};Password={1}", user, password);
            var connectionString = string.Format("Server={0};Database={1};{2}", server, database, credentials);
            var connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        protected static IDisposable TemporaryConsoleColorSuccess()
        {
            return TemporaryConsoleColor(ConsoleColor.Green);
        }

        protected static IDisposable TemporaryConsoleColorError()
        {
            return TemporaryConsoleColor(ConsoleColor.Red);
        }

        protected static IDisposable TemporaryConsoleColorWarning()
        {
            return TemporaryConsoleColor(ConsoleColor.Yellow);
        }

        private static IDisposable TemporaryConsoleColor(ConsoleColor color)
        {
            var value = Console.ForegroundColor;
            Console.ForegroundColor = color;
            return new Temp(() => Console.ForegroundColor = value);
        }

        private class Temp : IDisposable
        {
            private readonly Action _action;

            public Temp(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action();
            }
        }
    }
}