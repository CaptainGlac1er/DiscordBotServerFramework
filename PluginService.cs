using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.Interactions;
using GlacierByte.Discord.Server.Api;
using Microsoft.Extensions.DependencyInjection;
using CommandModuleInfo = Discord.Commands.ModuleInfo;
using InteractionsModuleInfo = Discord.Interactions.ModuleInfo;

namespace DiscordBotServer;

public sealed class PluginService
{
    private readonly Dictionary<String, IEnumerable<CommandModuleInfo>> loadedCommandModules = new();
    private readonly Dictionary<String, IEnumerable<InteractionsModuleInfo>> loadedInteractionsModules = new();
    private readonly Dictionary<Type, Type> _customServices = new();

    private readonly string _pathToPluginFolder;

    public PluginService(string pathToPlugins)
    {
        _pathToPluginFolder = pathToPlugins;
    }

    public event EventHandler<ServiceCollection> PluginUpdate;

    public void StartListeningToPluginFolder()
    {
        LoadPlugins();
        Console.WriteLine(_pathToPluginFolder);
        var folderWatcher = new FileSystemWatcher(_pathToPluginFolder);
        folderWatcher.NotifyFilter = NotifyFilters.Attributes
                                     | NotifyFilters.CreationTime
                                     | NotifyFilters.DirectoryName
                                     | NotifyFilters.FileName
                                     | NotifyFilters.LastWrite
                                     | NotifyFilters.Security
                                     | NotifyFilters.Size;

        folderWatcher.Changed += OnChanged;
        folderWatcher.Created += OnChanged;
        folderWatcher.Deleted += OnChanged;
        folderWatcher.Renamed += OnChanged;
        folderWatcher.Error += OnError;

        folderWatcher.Filter = "*.dll";
        folderWatcher.IncludeSubdirectories = true;
        folderWatcher.EnableRaisingEvents = true;
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine(e.ToString());
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"Changed: {e.FullPath}");
        LoadPlugins();
    }

    private void LoadPlugins()
    {
        var sc = new ServiceCollection();
        LoadServices(sc, out sc);
        OnPluginUpdate(sc);
    }

    private void LoadServices(ServiceCollection sc, out ServiceCollection pluginServicedLoadedServiceCollection)
    {
        Console.WriteLine("Starting to load Services");
        _customServices.Clear();
        var typeProcessor = new Dictionary<Type, Delegate>
        {
            {
                typeof(ICustomService), new Action<Type>(service =>
                    {
                        if (_customServices.ContainsKey(service)) return;
                        Console.WriteLine($"0. Adding Interface {service}");
                        _customServices.Add(service, service);
                    }
                )
            },
            {
                typeof(ISharedService), new Action<Type>(service =>
                    {
                        if (_customServices.ContainsKey(service)) return;
                        Console.WriteLine($"0. Adding Interface {service}");
                        _customServices.Add(service, service);
                    }
                )
            }
        };
        var assemblies = Directory
            .GetFiles(_pathToPluginFolder, "*.dll", SearchOption.AllDirectories)
            .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath)
            .ToList();

        foreach (var assembly in assemblies)
        {
            foreach (var customType in assembly.ExportedTypes)
            {
                foreach (var typeInterface in customType.GetInterfaces())
                {
                    Console.WriteLine($"0. Checking Interface {typeInterface}");
                    if (typeProcessor.ContainsKey(typeInterface))
                        typeProcessor[typeInterface].DynamicInvoke(customType);
                }
            }
            
        }

        foreach (var customService in _customServices.Values)
        {
            Console.WriteLine($"1. Adding Service {customService}");
            sc.AddSingleton(customService);
            Console.WriteLine($"1. Finished Adding Service {customService.Namespace} {customService.Name}");
        }

        Console.WriteLine("Finished loading Services");

        pluginServicedLoadedServiceCollection = sc;
    }

    public async Task LoadDiscordCommands(IServiceProvider dependencyInjectionServiceProvider,
        CommandService discordCommandService)
    {
        Console.WriteLine("Starting to load Commands");
        var assemblies = Directory
            .GetFiles(_pathToPluginFolder, "*.dll", SearchOption.AllDirectories)
            .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath)
            .ToList();
        
        foreach (var assembly in assemblies)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName == null)
            {
                Console.WriteLine($"Could not load assembly name from {assembly.FullName}");
                continue;
            }
            if (loadedCommandModules.ContainsKey(assemblyName))
            {
                var succesfulPluginRemoval = true;
                foreach (var commandModule in loadedCommandModules[assemblyName])
                {
                    var successfulModuleRemoval = await discordCommandService.RemoveModuleAsync(commandModule);
                    succesfulPluginRemoval = successfulModuleRemoval && succesfulPluginRemoval;
                    Console.WriteLine($"Remove Module: { commandModule.Name } result was {successfulModuleRemoval}");
                }
                Console.WriteLine($"Remove Plugin: { assembly.FullName } result was {succesfulPluginRemoval}");
            }
            var stuff =discordCommandService.AddModulesAsync(assembly, dependencyInjectionServiceProvider);
            var loadedCommandModule =  await  stuff;
            loadedCommandModules[assemblyName] = loadedCommandModule;
            foreach (var module in loadedCommandModule) Console.WriteLine($"adding Module {module.Name}");
        }

        Console.WriteLine("Finished loading Commands");
    }

    public async Task LoadDiscordInteractions(IServiceProvider dependencyInjectionServiceProvider,
        InteractionService discordInteractionService)
    {
        Console.WriteLine("Starting to load Interactions");
        var assemblies = Directory
            .GetFiles(_pathToPluginFolder, "*.dll", SearchOption.AllDirectories)
            .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath)
            .ToList();
        foreach (var assembly in assemblies)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName == null)
            {
                Console.WriteLine($"Could not load assembly name from {assembly.FullName}");
                continue;
            }
            if (loadedInteractionsModules.ContainsKey(assemblyName))
            {
                var successfulPluginRemoval = true;
                foreach (var commandModule in loadedInteractionsModules[assemblyName])
                {
                    var successfulModuleRemoval = await discordInteractionService.RemoveModuleAsync(commandModule);
                    successfulPluginRemoval = successfulModuleRemoval && successfulPluginRemoval;
                    Console.WriteLine($"Remove Module: {commandModule.Name} result was {successfulModuleRemoval}");
                }

                Console.WriteLine($"Remove Plugin: {assembly.FullName} result was {successfulPluginRemoval}");
            }

            var stuff = await discordInteractionService.AddModulesAsync(assembly,
                dependencyInjectionServiceProvider);
            var loadedInteractionModule = stuff.ToList();
            loadedInteractionsModules[assemblyName] = loadedInteractionModule;
        }

        Console.WriteLine("Finished loading Interactions");
    }

    private void OnPluginUpdate(ServiceCollection e)
    {
        PluginUpdate?.Invoke(this, e);
    }
}