namespace DbMigrator
{
	interface IValueConverter
	{
		string GetPatternOrNull();
		object Convert(string value);
	}
}