using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using GlacierByte.Discord.Server.Api;
using Microsoft.Extensions.DependencyInjection;

public class PluginService {
    Dictionary<Type, Type> CustomServices = new Dictionary<Type, Type>();

    private string PathToPluginFolder;

    public event EventHandler<ServiceCollection> PluginUpdate;

    public PluginService(string pathToPlugins) {
        PathToPluginFolder = pathToPlugins;
    }

    public void StartListeningToPluginFolder() {
        LoadPlugins();
        Console.WriteLine(PathToPluginFolder);
        var folderWatcher = new FileSystemWatcher(PathToPluginFolder);
        folderWatcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

        folderWatcher.Changed += OnChanged;

        folderWatcher.Filter = "*.dll";
        folderWatcher.IncludeSubdirectories = true;
        folderWatcher.EnableRaisingEvents = true;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"Changed: {e.FullPath}");
        LoadPlugins();
    }

    public void LoadPlugins() {
        ServiceCollection sc = new ServiceCollection();
        LoadServices(sc, out sc);
        OnPluginUpdate(sc);
    }

    private void LoadServices(ServiceCollection sc, out ServiceCollection pluginServicedLoadedServiceCollection) {
        Console.WriteLine("Starting to load Services");
        CustomServices.Clear();
        var typeProcessor = new Dictionary<Type, Delegate> {
            { 
                typeof(ICustomService), new Action<Type>(service => {
                        if(!CustomServices.ContainsKey(service)) {
                            Console.WriteLine($"0. Adding Interface {service}");
                            CustomServices.Add(service, service);
                        }
                    }
                )
            },
            { 
                typeof(ISharedService), new Action<Type>(service => {
                        if(!CustomServices.ContainsKey(service)) {
                            Console.WriteLine($"0. Adding Interface {service}");
                            CustomServices.Add(service, service);
                        }
                    }
                )
            }
        };
        var pluginDir = new DirectoryInfo(PathToPluginFolder);
        foreach (var plugin in pluginDir.GetFiles())
        {
            if(!plugin.FullName.Contains(".dll")) {
                Console.WriteLine($"Did not load {plugin.FullName}");
                continue;
            }
            // Console.WriteLine($"checking file for services {plugin.FullName}");
            var assembly = Assembly.LoadFile(plugin.FullName);

            foreach (Type customType in assembly.ExportedTypes)
            {
                foreach (var typeInterface in customType.GetInterfaces()) {
                    Console.WriteLine($"0. Checking Interface {typeInterface}");
                    if(typeProcessor.ContainsKey(typeInterface)) {
                        typeProcessor[typeInterface].DynamicInvoke(customType);
                    }
                }
            }
        }
        
        foreach (Type customService in CustomServices.Values)
        {
            Console.WriteLine($"1. Adding Service {customService}");
            sc.AddSingleton(customService);
            Console.WriteLine($"1. Finished Adding Service {customService.Namespace} {customService.Name}");
        }
        Console.WriteLine("Finished loading Services");

        pluginServicedLoadedServiceCollection = sc;
    }

    public IServiceProvider InitializeCustomServices(IServiceProvider sp) {
        foreach (Type customService in CustomServices.Values)
        {
            Console.WriteLine($"2. Creating Service {customService}");
            var service = sp.GetService(customService);
            Console.WriteLine($"2. Finished Creating Service {customService.Namespace} {customService.Name}");
        }
        return sp;
    }

    public async Task LoadDiscordCommands(IServiceProvider dependancyInjectionServiceProvider,Discord.Commands.CommandService discordCommandService) {
        // InitializeCustomServices(dependancyInjectionServiceProvider);
        Console.WriteLine("Starting to load Commands");
        var pluginDir = new DirectoryInfo(PathToPluginFolder);
        foreach (var plugin in pluginDir.GetFiles())
        {
            if(!plugin.FullName.Contains(".dll")) {
                Console.WriteLine($"Did not load {plugin.FullName}");
                continue;
            }
            // Console.WriteLine($"checking file for services {plugin.FullName}");
            var assembly = Assembly.LoadFile(plugin.FullName);
            var adding = discordCommandService.AddModulesAsync(assembly, dependancyInjectionServiceProvider);
            IEnumerable<Discord.Commands.ModuleInfo> stuff = await adding;
            foreach(var module in stuff)
            {
                Console.WriteLine($"adding Module {module.Name}");
            }

        }
        Console.WriteLine("Finished loading Commands");
    }
    
    public async Task LoadDiscordInteractions(IServiceProvider dependancyInjectionServiceProvider, Discord.Interactions.InteractionService discordInteractionService) {
        Console.WriteLine("Starting to load Interactions");
        var pluginDir = new DirectoryInfo(PathToPluginFolder);
        foreach(var plugin in pluginDir.GetFiles())
        {
            if(!plugin.FullName.Contains(".dll")) {
                Console.WriteLine($"Did not load {plugin.FullName}");
                continue;
            }
            var assembly = Assembly.LoadFile(plugin.FullName);
            foreach(var module in assembly.ExportedTypes) {
                await discordInteractionService.RemoveModuleAsync(module);
            }
            var adding = discordInteractionService.AddModulesAsync(assembly, dependancyInjectionServiceProvider);
            IEnumerable<Discord.Interactions.ModuleInfo> stuff = await adding;
            foreach(var module in stuff)
            {
                Console.WriteLine($"adding Interaction Interactions {module.Name}");
            }
        }
        Console.WriteLine("Finished loading Interactions");
    }

    protected virtual void OnPluginUpdate(ServiceCollection e)
    {
        PluginUpdate?.Invoke(this, e);
    }
}