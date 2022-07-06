using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;

public class InteractionHandler {
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interaction;
    private readonly IServiceProvider _services;

    public InteractionHandler(IServiceProvider services, InteractionService interactions, DiscordSocketClient client)
    {
        _interaction = interactions;
        _services = services;
        _client = client;
    }

    public async Task InitializeAsync()
    {
        /*var pluginDir = new DirectoryInfo("./plugins");
        foreach(var plugin in pluginDir.GetFiles())
        {
            if(!plugin.FullName.Contains(".dll")) {
                Console.WriteLine($"Did not load {plugin.FullName}");
                continue;
            }
            // Console.WriteLine($"Checking file for modules {plugin.FullName}");
            var assembly = Assembly.LoadFile(plugin.FullName);
            var adding = _interaction.AddModulesAsync(assembly,_services);
            IEnumerable<ModuleInfo> stuff = await adding;
            foreach(var module in stuff)
            {
                Console.WriteLine($"adding Interaction Module {module.Name}");
            }
        }*/
         _client.InteractionCreated += HandleInteractionAsync;
    }

    public async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        Console.WriteLine(interaction.ToString());
        if (!(interaction is SocketSlashCommand component)) return;
        Console.WriteLine($"'{component.Data}' read as a interaction");
        var result = await _interaction.ExecuteCommandAsync(
            context: new SocketInteractionContext(_client, component),
            services: _services
            );
    }

    public async Task InitCommands() {
        foreach (var guild in _client.Guilds) {
            try {
            await  _interaction.RegisterCommandsToGuildAsync(guild.Id);
            } catch (InvalidOperationException exception) {
                Console.WriteLine(exception.Message);
            }
        }
    }
}