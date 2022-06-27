using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

public class CommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;
    private readonly IServiceProvider _services;

    public CommandHandler(IServiceProvider services, CommandService commands, DiscordSocketClient client)
    {
        _commands = commands;
        _services = services;
        _client = client;
    }

    public async Task InitializeAsync()
    {
        var pluginDir = new DirectoryInfo("./plugins");
        foreach(var plugin in pluginDir.GetFiles())
        {
            if(!plugin.FullName.Contains(".dll")) {
                Console.WriteLine($"Did not load {plugin.FullName}");
                continue;
            }
            // Console.WriteLine($"Checking file for modules {plugin.FullName}");
            var assembly = Assembly.LoadFile(plugin.FullName);
            var adding = _commands.AddModulesAsync(assembly,_services);
            IEnumerable<ModuleInfo> stuff = await adding;
            foreach(var module in stuff)
            {
                Console.WriteLine($"adding Module {module.Name}");
            }
        }
         _client.MessageReceived += HandleCommandAsync;
    }

    public async Task HandleCommandAsync(SocketMessage msg)
    {
        if (!(msg is SocketUserMessage message)) return;
        if (message.Content.Length > 0 && message.Content.StartsWith("`"))
        {
            Console.WriteLine($"'{message.Content}' read as a command");
            var result = await _commands.ExecuteAsync(
                context: new SocketCommandContext(_client, message),
                argPos: 1,
                services: _services);

        }
    }
}
