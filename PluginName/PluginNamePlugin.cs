using System;
using System.Composition;
using System.Linq;
using System.Reflection;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using JetBrains.Annotations;

namespace PluginName {
	[Export(typeof(IPlugin))]
	[UsedImplicitly]
	public class PluginNamePlugin : IPlugin {
		public void OnLoaded() {
			Assembly assembly = Assembly.GetExecutingAssembly();
			string repository = assembly
				.GetCustomAttributes<AssemblyMetadataAttribute>()
				.First(x => x.Key == "RepositoryUrl")
				.Value ?? throw new InvalidOperationException(nameof(AssemblyMetadataAttribute));

			const string GitSuffix = ".git";
			int index = repository.IndexOf(GitSuffix, StringComparison.Ordinal);
			if (index >= 0) {
				repository = repository[..(index + 1)];
			}

			string company = assembly
				.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? throw new InvalidOperationException(nameof(AssemblyCompanyAttribute));

			ASF.ArchiLogger.LogGenericInfo(Name + " by " + company + " | Support & source code: " + repository);
		}

		public string Name => nameof(PluginName);
		public Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? throw new InvalidOperationException();

	}
}
