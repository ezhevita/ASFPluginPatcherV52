using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam.Interaction;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace ASFPluginPatcherV52;

[UsedImplicitly]
public class PatcherPlugin : IPlugin
{
	static PatcherPlugin()
	{
		Runner();
	}
	public Task OnLoaded() => Task.CompletedTask;
	private static readonly string ASFPluginNamespace = typeof(IPlugin).Namespace!;
	private static readonly Version RequiredVersion = new(5, 2, 1);

	private static readonly HashSet<Type> InterfacesToScan = new()
	{
		typeof(IASF),
		typeof(IBot),
		typeof(IBotCardsFarmerInfo),
		typeof(IBotConnection),
		typeof(IBotModules),
		typeof(IBotSteamClient),
		typeof(IBotTradeOfferResults),
		typeof(IBotUserNotifications),
		typeof(IPlugin),
		typeof(ISteamPICSChanges)
	};

	private static void Runner()
	{
		if (typeof(ASF).Assembly.GetName().Version < RequiredVersion)
			return;

		ASF.ArchiLogger.LogGenericInfo("ASF V5.2.1.0+ Plugin Patcher for old plugins, made by ezhevita");
		ASF.ArchiLogger.LogGenericInfo("Support & source code: https://github.com/ezhevita/ASFPluginPatcherV52");
		ASF.ArchiLogger.LogGenericInfo("Discovering plugins...");
		var assemblies = PrivateMemberExtensions.LoadAssemblies();
		var shouldRestart = false;
		foreach (var assembly in assemblies)
		{
			shouldRestart |= PatchAssembly(assembly);
		}

		if (shouldRestart)
		{
			Actions.Restart();
		}
	}

	internal static bool PatchAssembly(Assembly assembly)
	{
		var referencedAssembly = assembly.GetReferencedAssemblies().FirstOrDefault(x => x.Name == nameof(ArchiSteamFarm) && x.Version < RequiredVersion);

		if (referencedAssembly == null)
			return false;

		var assemblyName = assembly.GetName().Name;
		ASF.ArchiLogger.LogGenericInfo($"Migrating {assemblyName}...");
		using var module = ModuleDefinition.ReadModule(assembly.Location);

		module.AssemblyReferences.First(x => x.Name == nameof(ArchiSteamFarm) && x.Version < RequiredVersion).Version = RequiredVersion;
		var completedTaskMethod = module.ImportReference(typeof(Task).GetProperty(nameof(Task.CompletedTask))!.GetMethod);
		var patchedAnything = false;

		foreach (var type in module.Types)
		{
			patchedAnything |= PatchType(type, module, completedTaskMethod);
		}

		if (patchedAnything)
		{
			module.Write(assembly.Location + ".tmp");

			try
			{
				File.Delete(assembly.Location);
				File.Move(assembly.Location + ".tmp", assembly.Location);
				ASF.ArchiLogger.LogGenericInfo($"Successfully migrated {assemblyName}!");
			} catch (IOException e)
			{
				ASF.ArchiLogger.LogGenericWarningException(e);
				ASF.ArchiLogger.LogGenericWarning($"Could not automatically replace {assemblyName} file! Please replace it by yourself.");
#pragma warning disable CA1031
			} catch (Exception e)
#pragma warning restore CA1031
			{
				ASF.ArchiLogger.LogGenericException(e);
				ASF.ArchiLogger.LogGenericWarning($"Error occured while trying to replace {assemblyName} file!");
			}
		} else
		{
			ASF.ArchiLogger.LogGenericInfo($"Nothing to patch in {assemblyName}!");
		}

		return patchedAnything;
	}

	private static bool PatchType(TypeDefinition type, ModuleDefinition module, MethodReference completedTaskMethod)
	{
		if (!type.Interfaces.Any(x => x.InterfaceType.Name == nameof(IPlugin) && x.InterfaceType.Namespace == ASFPluginNamespace))
			return false;

		var patchedAnything = false;
		foreach (var pluginInterface in InterfacesToScan)
		{
			if (!type.Interfaces.Any(x => x.InterfaceType.Name == pluginInterface.Name && x.InterfaceType.Namespace == ASFPluginNamespace))
				continue;

			var taskType = module.ImportReference(typeof(Task));
			var taskGenericType = module.ImportReference(typeof(Task<>));
			var methods = pluginInterface.GetMethods();
			foreach (var interfaceMethod in methods)
			{
				if (!(interfaceMethod.ReturnType == typeof(Task) || interfaceMethod.ReturnType.IsGenericType && interfaceMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
					continue;
				
				var pluginMethod = type.Methods.First(x => x.Name == interfaceMethod.Name);
				var isGeneric = interfaceMethod.ReturnType != typeof(Task);
				var methodToInsert = !isGeneric ? completedTaskMethod : module.ImportReference(
					typeof(Task)
						.GetMethod(nameof(Task.FromResult), BindingFlags.Static | BindingFlags.Public)!
						.MakeGenericMethod(interfaceMethod.ReturnType.GenericTypeArguments)
				);

				pluginMethod.ReturnType = !isGeneric ? taskType : taskGenericType.MakeGenericInstanceType(pluginMethod.ReturnType);
				var processor = pluginMethod.Body.GetILProcessor();
				var returnInstructions = processor.Body.Instructions.Where(x => x.OpCode == OpCodes.Ret).ToList();
				if (returnInstructions.Count > 0)
				{
					patchedAnything = true;
				}

				foreach (var instruction in returnInstructions)
				{
					var taskInstruction = Instruction.Create(OpCodes.Call, methodToInsert);
					processor.InsertBefore(instruction, taskInstruction);
					foreach (var referenceInstruction in processor.Body.Instructions.Where(x => x.Operand is Instruction branch && branch == instruction))
					{
						referenceInstruction.Operand = taskInstruction;
					}
				}
			}
		}

		return patchedAnything;
	}

	public string Name => "ASFPluginPatcherV5.2";
	public Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? throw new InvalidOperationException(nameof(Version));
}
