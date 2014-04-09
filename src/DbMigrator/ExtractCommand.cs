using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DbMigrator
{
	class ExtractCommand : Command
	{
		protected override IEnumerable<Option> GetOptions()
		{
			return new List<Option>(base.GetOptions())
				       {
					       new NamedOption("directory") {Shortcut = "dir", IsRequired = true},
							 new NamedOption("quiet"){IsFlag = true, Shortcut = "q"}
				       };
		}

		protected override void Execute(Args args)
		{
			var root = args.GetValue("directory");
			var server = args.GetValue("server");
			var database = args.GetValue("database");

			if (args.Contains("quiet"))
			{
				Console.WriteLine("Exctracting artefacts from {0} {1}", server, database);
			}
			else
			{
				Console.Write("Exctract artefacts from {0} {1} (y/n)? ", server, database);
				if (!Console.ReadLine().Equals("y", StringComparison.CurrentCultureIgnoreCase)) return;
			}

			var filters = new[]
				              {
					              "^_.+$",
					              "^(sp|fn)_.*diagram.*$",
					              "^.+_dts_.+$",
					              "^.+_gen_.+$",
					              "^conv_.+$",
									  "^IsScriptExecuted$",
									  "^syncobj_.+$",
									  "^sysextendedarticlesview$"
				              };
			var types = new[]
				            {
					            new TypeDef{Key = "FN", Name = "function", Dir = "Functions"},
					            new TypeDef{Key = "P",  Name = "procedure", Dir = "StoredProcedures"},
					            new TypeDef{Key = "V",  Name = "view", Dir = "Views"}
				            };

			var schemas = Database.Query<Item>("select * from sys.schemas").ToDictionary(x => x.schema_id, x => x.name);
			foreach (var type in types)
			{
				var dir = Path.Combine(root, type.Dir);
				MakeSureDirectoryExists(dir);

				var files = Directory.GetFiles(dir, "*.sql", SearchOption.AllDirectories).ToList();
				foreach (var file in files.ToList())
				{
					var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
					if (!filters.Any(x => Regex.IsMatch(fileNameWithoutExtension, x, RegexOptions.IgnoreCase))) continue;
					RemoveReadOnlyAttribute(file);
					File.Delete(file);
					files.Remove(file);

				}
				using (TemporaryConsoleColorError())
					foreach (var @group in files.GroupBy(x => Path.GetFileName(x).ToLower()).Where(x => x.Count() > 1))
					{
						Console.WriteLine("duplicate filenames");
						foreach (var file in @group)
							Console.WriteLine("  " + file);
					}

				var filesByLowerCaseFileName = files.ToDictionary(x => Path.GetFileName(x).ToLower());
				var selectItemsSql = string.Format("select name, schema_id from sys.objects where type = '{0}'", type.Key);
				foreach (var item in Database.Query<Item>(selectItemsSql))
				{
					if (filters.Any(x => Regex.IsMatch(item.name, x, RegexOptions.IgnoreCase))) continue;
					var schema = schemas[item.schema_id];
					var filename = (schema == "dbo" ? item.name : schema + "." + item.name) + ".sql";
					string targetPath;
					if (!filesByLowerCaseFileName.TryGetValue(filename.ToLower(), out targetPath))
						targetPath = Path.Combine(dir, filename);
					RemoveReadOnlyAttribute(targetPath);
					File.WriteAllText(targetPath, CreateScript(schema, item.name, type));
					filesByLowerCaseFileName.Remove(filename.ToLower());
				}
				foreach (var file in filesByLowerCaseFileName.Values)
				{
					RemoveReadOnlyAttribute(file);
					File.Delete(file);
				}
			}
		}

		private static void RemoveReadOnlyAttribute(string path)
		{
			if (!File.Exists(path)) return;
			var attributes = File.GetAttributes(path);
			File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
		}

		private static void MakeSureDirectoryExists(string dir)
		{
			if (Directory.Exists(dir)) return;
			MakeSureDirectoryExists(Path.GetDirectoryName(dir));
			Directory.CreateDirectory(dir);
		}

		private string CreateScript(string schema, string name, TypeDef type)
		{
			var schemaAndName = string.Format("[{0}].[{1}]", schema, name);
			var ifItemExists = string.Format("if exists (select * from sys.objects where object_id = OBJECT_ID(N'{0}') and type = N'{1}')", schemaAndName, type.Key);
			return new StringBuilder()
				.AppendLine(ifItemExists)
				.AppendLine(string.Format("drop {0} {1}", type.Name, schemaAndName))
				.AppendLine("go")
				.AppendLine()
				.AppendLine(GetContentFromDb(schemaAndName, type))
				.AppendLine()
				.AppendLine("go")
				.AppendLine()
				.ToString();
		}

		private string GetContentFromDb(string schemaAndName, TypeDef type)
		{
			var builder = new StringBuilder();
			var lines = Database.Query<ContentRow>(string.Format("exec sp_HelpText '{0}'", schemaAndName));
			foreach (var line in lines)
				builder.AppendLine(line.Text.TrimEnd());
			return builder.ToString().Trim();
		}

		class TypeDef
		{
			public string Key { get; set; }
			public string Name { get; set; }
			public string Dir { get; set; }
		}

		class ContentRow
		{
			public string Text { get; set; }
		}

		class Item
		{
			public string name { get; set; }
			public int schema_id { get; set; }

			public override string ToString()
			{
				return name;
			}
		}
	}
}