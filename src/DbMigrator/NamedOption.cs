namespace DbMigrator
{
	class NamedOption : Option
	{
		public NamedOption(string name) : base(name) { }

		public string Shortcut { get; set; }
	}
}