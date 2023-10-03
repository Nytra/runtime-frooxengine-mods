// This file is part of NeosModConfig and is licensed under the GNU LGPL v3.0.
// See LICENSE.txt file for full text.
// Copyright © 2023 Michael Ripley.

namespace NeosModConfig
{
	/// <summary>
	/// Represents the data for the <see cref="ModConfiguration.OnThisConfigurationChanged"/> and <see cref="ModConfiguration.OnAnyConfigurationChanged"/> events.
	/// </summary>
	public class ConfigurationChangedEvent
	{
		/// <summary>
		/// The <see cref="ModConfiguration"/> in which the change occured.
		/// </summary>
		public ModConfiguration Config { get; private set; }

		/// <summary>
		/// The specific <see cref="ModConfigurationKey{T}"/> who's value changed.
		/// </summary>
		public ModConfigurationKey Key { get; private set; }

		/// <summary>
		/// A custom label that may be set by whoever changed the configuration.
		/// </summary>
		public string? Label { get; private set; }

		internal ConfigurationChangedEvent(ModConfiguration config, ModConfigurationKey key, string? label)
		{
			Config = config;
			Key = key;
			Label = label;
		}
	}
}
