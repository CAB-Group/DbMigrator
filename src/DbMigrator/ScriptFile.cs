using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DbMigrator
{
    class ScriptFile : ScriptFileBase
    {
        public ScriptFile(string path)
            : base(path)
        {
        }

        public string FileName
        {
            get { return System.IO.Path.GetFileName(Path); }
        }

        public string VersionAndNumber
        {
            get { return string.Format("{0}_{1}.sql", Version, Number); }
        }

        public string Version
        {
            get { return GetNamePart(0); }
        }

        public int Number
        {
            get { return int.Parse(GetNamePart(1)); }
        }

        private string GetNamePart(int partIndex)
        {
            return System.IO.Path.GetFileNameWithoutExtension(Path).Split('_')[partIndex];
        }

        public static IEnumerable<ScriptFile> Load(string directory, bool recursive = false)
        {
            var pattern = string.Format(@"^{0}_\d+\.sql$", string.Join(@"\.", Enumerable.Repeat(@"\d+", VersionNumber.Numbers)));
            var result = from path in Directory.GetFiles(directory)
                         let name = System.IO.Path.GetFileName(path)
                         where Regex.IsMatch(name, pattern)
                         let script = new ScriptFile(path)
                         orderby script.Version, script.Number
                         select script;

            return recursive ? result.Concat(Directory.GetDirectories(directory).SelectMany(dir => Load(dir, true))) : result;
        }

        public Version GetComparableVersion()
        {
            return new Version(Version + Number);
        }

        protected override string[] GetForbiddenPatterns()
        {
            return new[] { @"^\s*begin\s+tran", @"^\s*use\s" };
        }
    }
}