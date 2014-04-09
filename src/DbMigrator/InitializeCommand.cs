using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DbMigrator
{
    class InitializeCommand : Command
    {
        protected override IEnumerable<Option> GetOptions()
        {
            return new List<Option>(base.GetOptions())
				       {
					       new NamedOption("conflict") {Converter = new EnumConverter<ConflictStrategy>()}
				       };
        }

        protected override void Execute(Args args)
        {
            var server = args.GetValue("server");
            var database = args.GetValue("database");
            Console.WriteLine("Initializing server:{0} database:{1}", server, database);

            const string querySql = "select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA = 'dbo' and TABLE_NAME = 'ScriptLog'";
            if (Database.Query(querySql).Any())
            {
                switch (args.GetValueOrDefault("conflict", ConflictStrategy.Fail))
                {
                    case ConflictStrategy.Fail:
                        throw new ApplicationException("Database already initialized");
                    case ConflictStrategy.Notify:
                        using (TemporaryConsoleColorWarning())
                            Console.WriteLine("Database already initialized");
                        return;
                    case ConflictStrategy.Ignore:
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var createTable = new StringBuilder()
                .AppendLine("create table [dbo].[ScriptLog] (")
                .AppendLine("  [Id] int not null identity (1,1),")
                .AppendLine("  [Version] nvarchar(10) not null,")
                .AppendLine("  [ScriptNumber] int null,")
                .AppendLine("  [User] nvarchar(100) not null,")
                .AppendLine("  [Date] datetimeoffset(7) not null")
                .AppendLine(")")
                .ToString();

            var addPrimaryKey = new StringBuilder()
                .AppendLine("alter table [dbo].[ScriptLog] ADD CONSTRAINT")
                .AppendLine("PK_ScriptLog PRIMARY KEY CLUSTERED (")
                .AppendLine("  [Id]")
                .AppendLine(") WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]")
                .ToString();

            const string createIndex1 = "create unique index [ix_ScriptLog_Version] on [dbo].[ScriptLog] ([Version]) where [ScriptNumber] is null";
            const string createIndex2 = "create unique index [ix_ScriptLog_Version_ScriptNumber] on [dbo].[ScriptLog] ([Version], [ScriptNumber]) where [ScriptNumber] is not null";

            var createFunction = new StringBuilder()
                .AppendLine("create function [dbo].[IsScriptExecuted] (")
                .AppendLine("	@versionMajor int,")
                .AppendLine("	@versionMinor int,")
                .AppendLine("	@versionRevision int,")
                .AppendLine("	@scriptNumber int")
                .AppendLine(")")
                .AppendLine("returns bit")
                .AppendLine("as")
                .AppendLine("begin")
                .AppendLine("	declare @version nvarchar(10)")
                .AppendLine("	set @version = cast(@versionMajor as nvarchar(5)) + '.' + cast(@versionMinor as nvarchar(5)) + '.' + cast(@versionRevision as nvarchar(5))")
                .AppendLine("	if exists (select * from [dbo].[ScriptLog] where [Version] = @version and [ScriptNumber] = @scriptNumber)")
                .AppendLine("	begin")
                .AppendLine("		return 1")
                .AppendLine("	end")
                .AppendLine("	return 0")
                .AppendLine("end")
                .ToString();

            const string insert = "insert [dbo].[ScriptLog] ([Version], [User], [Date]) values (@version, @user, @date)";
            var entry = new
                        {
                            Date = DateTimeOffset.UtcNow,
                            User = Environment.UserName,
                            Version = new VersionNumber().ToString()
                        };

            Database.Execute(x =>
                             {
                                 x.Execute(createTable);
                                 x.Execute(addPrimaryKey);
                                 x.Execute(createIndex1);
                                 x.Execute(createIndex2);
                                 x.Execute(createFunction);
                                 x.Execute(insert, entry);
                             });

            using (TemporaryConsoleColorSuccess())
                Console.WriteLine("Done");
        }
    }
}