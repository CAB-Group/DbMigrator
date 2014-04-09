using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DbMigrator
{
    abstract class ScriptFileBase
    {
        protected ScriptFileBase(string path)
        {
            Path = path;
        }

        public string Path { get; private set; }

        public IEnumerable<Batch> GetBatches()
        {
            var fromLine = 1;
            var toLine = 0;
            var builder = new StringBuilder();

            var lines = File.ReadAllLines(Path);

            foreach (var line in lines)
            {
                var patterns = GetForbiddenPatterns();
                if (patterns.Any(pattern => Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase)))
                    throw new InvalidOperationException("invalid statement: " + line);

                toLine++;

                if (!"go".Equals(line.Trim(), StringComparison.InvariantCultureIgnoreCase))
                {
                    builder.AppendLine(line);
                    continue;
                }

                yield return new Batch
                                     {
                                         FromLine = fromLine,
                                         ToLine = toLine,
                                         Sql = builder.ToString()
                                     };
                fromLine = toLine + 1;
                builder.Clear();
            }

            if (!string.IsNullOrWhiteSpace(builder.ToString()))
                yield return new Batch
                                     {
                                         FromLine = fromLine,
                                         ToLine = toLine,
                                         Sql = builder.ToString()
                                     };
        }

        protected abstract string[] GetForbiddenPatterns();

        public IEnumerable<ScriptFileBase> GetIncludes()
        {
            const string prefix = "--#include ";
            var directory = System.IO.Path.GetDirectoryName(Path);

            return from line in File.ReadAllLines(Path)
                   where line.StartsWith(prefix)
                   select line.Substring(prefix.Length)
                       into relativePath
                       select System.IO.Path.Combine(directory, relativePath)
                           into includedFilePath
                           select new IncludedFile(includedFilePath);
        }

        public class Batch
        {
            public int FromLine { get; set; }
            public int ToLine { get; set; }
            public string Sql { get; set; }
        }

        class IncludedFile : ScriptFileBase
        {
            public IncludedFile(string path)
                : base(path)
            {
            }

            protected override string[] GetForbiddenPatterns()
            {
                return new[] { @"^\s*use\s" };
            }
        }
    }
}