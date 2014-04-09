namespace DbMigrator
{
	abstract class Option
	{
		protected Option(string name)
		{
			Name = name;
			Converter = new NoConverter();
		}

		public string Name { get; private set; }
		public string Description { get; set; }
		public bool IsRequired { get; set; }
		public bool IsFlag { get; set; }
		public IValueConverter Converter { get; set; }

		class NoConverter : IValueConverter
		{
			public string GetPatternOrNull()
			{
				return null;
			}

			public object Convert(string value)
			{
				return value;
			}
		}
	}
}