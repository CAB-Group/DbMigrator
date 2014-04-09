using System.Collections.Generic;

namespace DbMigrator
{
	class SetSysVersionCommand : Command
	{
		protected override IEnumerable<Option> GetOptions()
		{
			return new List<Option>(base.GetOptions())
				       {
					       new NamedOption("version") { IsRequired = true, Shortcut = "ver" }
				       };
		}

		protected override void Execute(Args args)
		{
			Database.Execute(
				@"MERGE DatabaseVersion AS target 
    USING (SELECT @VersionNumber) AS source (VersionNumber)
    ON (target.VersionNumber = source.VersionNumber)
    WHEN MATCHED THEN 
        UPDATE SET InsertedDate = sysdatetime()
	WHEN NOT MATCHED THEN	
	    INSERT (VersionNumber, DescriptionText)
	    VALUES (source.VersionNumber, 'DbMigrator');",
				new {VersionNumber = args.GetValue("version")});
		}
	}
}
