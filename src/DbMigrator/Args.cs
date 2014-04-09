using System;
using System.Collections.Generic;
using System.Linq;

namespace DbMigrator
{
	class Args
	{
		private readonly Dictionary<string, IValueConverter> _converters = new Dictionary<string, IValueConverter>();
		private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

		public Args(IEnumerable<string> args, IEnumerable<Option> options)
		{
			_converters = options.ToDictionary(x => x.Name, x => x.Converter);

			args = args.ToList();
			options = options.ToList();
			var indexedOptionIndex = 0;
			var indexedOptions = options.OfType<IndexedOption>().ToList();
			var shortcutToNameDictionary = options.OfType<NamedOption>().Where(x => !string.IsNullOrEmpty(x.Shortcut)).ToDictionary(x => x.Shortcut.ToLower(), x => x.Name);
			var namedOptionNames = new HashSet<string>(options.OfType<NamedOption>().Select(x => x.Name.ToLower()));

			foreach (var arg in args)
			{
				if (!arg.StartsWith("-") && !arg.StartsWith("/"))
				{
					if (indexedOptionIndex == indexedOptions.Count) throw new ArgumentException("Unknown arg: " + arg);
					var indexedOption = indexedOptions[indexedOptionIndex];
					_values.Add(indexedOption.Name.ToLower(), arg);
					indexedOptionIndex++;
					continue;
				}

				var splitIndex = arg.IndexOf(":", StringComparison.CurrentCulture);
				var key = (splitIndex == -1 ? arg.Substring(1) : arg.Substring(1, splitIndex - 1)).ToLower();
				string name;
				name = shortcutToNameDictionary.TryGetValue(key, out name) ? name : key;
				if (!namedOptionNames.Contains(name)) throw new ArgumentException("Unknown arg: " + arg);
				var value = splitIndex == -1 ? string.Empty : arg.Substring(splitIndex + 1);
				_values.Add(name.ToLower(), value);
			}

			foreach (var option in options.Where(x => x.IsRequired && !_values.ContainsKey(x.Name.ToLower())))
				throw new ArgumentException("Missing arg: " + option.Name);
		}

		public string GetValue(string key)
		{
			string value;
			if (_values.TryGetValue(key.ToLower(), out value))
				return value;

			throw new ArgumentException("Argument not found.", key);
		}

		public T GetValue<T>(string key)
		{
			return (T)_converters[key].Convert(GetValue(key));
		}

		public string GetValueOrDefault(string key, string @default)
		{
			string value;
			return TryGetValue(key, out value) ? value : @default;
		}

		public T GetValueOrDefault<T>(string key, T @default)
		{
			T value;
			return TryGetValue<T>(key, out value) ? value : @default;
		}

		public string GetValueOrNull(string key)
		{
			return GetValueOrDefault(key, null);
		}

		public bool Contains(params string[] keys)
		{
			return keys.Any(x => _values.ContainsKey(x));
		}

		public bool TryGetValue(string key, out string value)
		{
			return _values.TryGetValue(key.ToLower(), out value);
		}

		public bool TryGetValue<T>(string key, out T value)
		{
			var found = Contains(key);
			value = found ? (T)_converters[key].Convert(GetValue(key)) : default(T);
			return found;
		}
	}
}