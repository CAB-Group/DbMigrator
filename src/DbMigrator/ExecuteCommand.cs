using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DbMigrator
{
    class ExecuteCommand : Command
    {
        protected override IEnumerable<Option> GetOptions()
        {
            return new List<Option>(base.GetOptions())
				       {
					       new NamedOption("directory") { IsRequired = true, Shortcut = "dir" },
					       new NamedOption("quiet") {Shortcut = "q", IsFlag = true },
					       new NamedOption("recursive") {Shortcut = "r", IsFlag = true }
				       };
        }

        protected override void Execute(Args args)
        {
            Console.WriteLine();
            Console.WriteLine("Server   " + args.GetValue("server"));
            Console.WriteLine("Database " + args.GetValue("database"));

            const string isInitialized =
               "select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA = 'dbo' and TABLE_NAME = 'ScriptLog'";
            if (!Database.Query(isInitialized).Any()) throw new ApplicationException("Database has not been intialized");

            var scriptLogEntries = ScriptLogEntry.Load(Database).ToList();
            var lastScriptLogEntry = GetLastScriptLogEntry(scriptLogEntries);
            var executedScriptFileNames = GetExecutedScriptFileNames(scriptLogEntries);

            var directory = args.GetValue("directory");
            var recursive = args.Contains("recursive");
            var scriptsNotExecuted = ScriptFile.Load(directory, recursive)
                                               .Where(x => !executedScriptFileNames.Contains(x.VersionAndNumber))
                                               .ToList();
            var obsoleteScripts =
               scriptsNotExecuted.Where(
                                        x =>
                                        x.GetComparableVersion() <
                                        lastScriptLogEntry.GetComparableVersion(IncludeScriptNumber.Yes)).ToList();
            var newScripts =
               scriptsNotExecuted.Where(
                                        x =>
                                        x.GetComparableVersion() >
                                        lastScriptLogEntry.GetComparableVersion(IncludeScriptNumber.No)).ToList();

            Console.WriteLine();
            Console.Write("Current version " + lastScriptLogEntry.Version);
            if (lastScriptLogEntry.ScriptNumber.HasValue)
                Console.Write("_" + lastScriptLogEntry.ScriptNumber);
            Console.WriteLine();

            if (obsoleteScripts.Any())
            {
                using (TemporaryConsoleColorWarning())
                {
                    Console.WriteLine();
                    Console.WriteLine("Scripts that won't be executed");
                    foreach (var scriptFile in obsoleteScripts)
                        Console.WriteLine("  {0}", scriptFile.FileName);
                }
            }

            if (!newScripts.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Did not find any scripts to execute");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Scripts that will be executed");
            foreach (var scriptFile in newScripts)
            {
                Console.Write("  {0}", scriptFile.FileName);
                if (scriptFile.Version == lastScriptLogEntry.Version &&
                    scriptFile.Number < lastScriptLogEntry.ScriptNumber.GetValueOrDefault())
                    using (TemporaryConsoleColorWarning())
                        Console.Write(" - older than current version");
                Console.WriteLine();
            }

            if (!args.Contains("quiet"))
            {
                Console.WriteLine();
                Console.Write("Continue (y/n): ");
                if (!"y".Equals(Console.ReadLine(), StringComparison.CurrentCultureIgnoreCase))
                    return;
            }

            var includesNotFound = new List<string>();
            var includesWithErrors = new Dictionary<string, IncludedScriptState>(StringComparer.CurrentCultureIgnoreCase);

            var stopwatch = Stopwatch.StartNew();
            foreach (var scriptFile in newScripts)
            {
                var scriptFileStopwatch = Stopwatch.StartNew();
                Console.WriteLine();
                Console.WriteLine("{0}", scriptFile.FileName);
                Database.Execute(x =>
                                 {
                                     foreach (var batch in scriptFile.GetBatches())
                                     {
                                         var batchStopwatch = Stopwatch.StartNew();
                                         var result = x.Execute(batch.Sql);
                                         Console.WriteLine("  lines {0}-{1} affected {2} rows, executed in {3}",
                                                           batch.FromLine,
                                                           batch.ToLine,
                                                           result,
                                                           Format(batchStopwatch.Elapsed));
                                     }

                                     foreach (var includedScript in scriptFile.GetIncludes())
                                     {
                                         try
                                         {
                                             Console.Write("  include {0}", includedScript.Path);
                                             foreach (var batch in includedScript.GetBatches())
                                                 x.Execute(batch.Sql);
                                             Console.WriteLine();
                                             if (includesWithErrors.ContainsKey(includedScript.Path))
                                                 includesWithErrors[includedScript.Path] = IncludedScriptState.Applied;
                                         }
                                         catch (Exception exception)
                                         {
                                             using (TemporaryConsoleColorWarning())
                                             {
                                                 if (exception is FileNotFoundException)
                                                 {
                                                     includesNotFound.Add(includedScript.Path);
                                                     Console.WriteLine(" not found");
                                                 }
                                                 else
                                                 {
                                                     includesWithErrors[includedScript.Path] = IncludedScriptState.NotApplied;
                                                     Console.WriteLine(" failed");
                                                     Console.WriteLine("    {0}", exception.Message);
                                                 }
                                             }
                                         }
                                     }

                                     const string sql =
                                        "insert [dbo].[ScriptLog] ([Version], [ScriptNumber], [User], [Date]) values (@version, @scriptNumber, @user, @date)";
                                     var entry = new ScriptLogEntry
                                                 {
                                                     Date = DateTimeOffset.UtcNow,
                                                     ScriptNumber = scriptFile.Number,
                                                     User = Environment.UserName,
                                                     Version = scriptFile.Version
                                                 };
                                     x.Execute(sql, entry);
                                 });

                Console.WriteLine("  script executed in {0}", Format(scriptFileStopwatch.Elapsed));
                Console.WriteLine();
            }

            if (includesNotFound.Any())
            {
                using (TemporaryConsoleColorWarning())
                {
                    Console.WriteLine("The following includes could not be found");
                    foreach (var item in includesNotFound.Distinct(StringComparer.CurrentCultureIgnoreCase))
                        Console.WriteLine("  {0}", item);
                    Console.WriteLine();
                }
            }

            if (includesWithErrors.Any())
            {
                var failedIncludes = includesWithErrors.Where(x => x.Value == IncludedScriptState.NotApplied)
                                                       .Select(x => x.Key)
                                                       .ToList();
                if (failedIncludes.Any())
                {
                    using (TemporaryConsoleColorError())
                    {
                        Console.WriteLine("The following includes could not be applied");
                        foreach (var item in failedIncludes)
                            Console.WriteLine("  {0}", item);
                        Console.WriteLine();
                    }
                }
                else
                {
                    using (TemporaryConsoleColorSuccess())
                    {
                        Console.WriteLine("All failed includes has been successfully applied by later scripts.");
                        Console.WriteLine();
                    }
                }
            }

            using (TemporaryConsoleColorSuccess())
                Console.WriteLine("Scripts executed in {0}", Format(stopwatch.Elapsed));
        }

        private static List<string> GetExecutedScriptFileNames(List<ScriptLogEntry> scriptLogEntries)
        {
            return scriptLogEntries
               .Select(x => x.ScriptName)
               .ToList();
        }

        private static ScriptLogEntry GetLastScriptLogEntry(List<ScriptLogEntry> scriptLogEntries)
        {
            return scriptLogEntries
               .OrderBy(x => x.GetComparableVersion(IncludeScriptNumber.Yes))
               .Last();
        }

        private static string Format(TimeSpan timespan)
        {
            var parts = new[]
                        {
                            new {value = timespan.Hours, unit = "h"},
                            new {value = timespan.Minutes, unit = "m"},
                            new {value = timespan.Seconds, unit = "s"},
                            new {value = timespan.Milliseconds, unit = "ms"}
                        }.SkipWhile(x => x.value == 0).ToList();

            return parts.Any()
                       ? string.Join(" ", parts.Select(x => string.Format("{0} {1}", x.value, x.unit)))
                       : "0 ms";
        }

        private enum IncludedScriptState
        {
            Applied,
            NotApplied
        }
    }
}
