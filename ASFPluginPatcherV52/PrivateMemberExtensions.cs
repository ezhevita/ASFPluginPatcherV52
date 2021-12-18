using System;
using System.Collections.Generic;
using System.Reflection;
using ArchiSteamFarm.Core;

namespace ASFPluginPatcherV52;

public static class PrivateMemberExtensions
{
	internal static HashSet<Assembly> LoadAssemblies() => (HashSet<Assembly>) typeof(ASF).Assembly
		.GetType("ArchiSteamFarm.Plugins.PluginsCore")!
		.GetMethod("LoadAssemblies", BindingFlags.Static | BindingFlags.NonPublic)!
		.Invoke(null, Array.Empty<object?>())!;

}
