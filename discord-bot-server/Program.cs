using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;
using GlacierByte.Discord.Server.Api;

namespace DiscordBotServer
{
    class Program
    {
        public Program(string token)
        {
            Token = token;
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
                AlwaysDownloadUsers = true,
                MessageCacheSize = 0
            });
            _commands = new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false, ThrowOnError = true });
        }
        private readonly string Token;
        static void Main(string[] args)
        {
            var configDef = new { DiscordToken = "" };
            var config = JsonConvert.DeserializeAnonymousType(File.ReadAllText(@"./config/discord.json"), configDef);
            Console.WriteLine(config.DiscordToken);
            new Program(config.DiscordToken).MainAsync().GetAwaiter().GetResult();
        }

        private readonly CommandService _commands;
        private readonly DiscordSocketClient _client;

        private List<Type> CustomServices = new List<Type>();

        public async Task MainAsync()
        {
            var sc = new ServiceCollection();
            LoadCustomServices();
            sc = AddPluginServices(sc);
            var services = BuildServiceProvider(sc);
            services = CreateCustomServices(services);
            await services.GetRequiredService<CommandHandler>().InitializeAsync();

            _client.Log += Log;
            _client.MessageReceived += MessageReceived;
            _client.UserVoiceStateUpdated += _client_UserVoiceStateUpdated;
            await _client.LoginAsync(TokenType.Bot, Token);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private Task _client_UserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            Console.WriteLine(arg1.ToString() + arg2.ToString() + arg3.ToString());
            return Task.CompletedTask;
        }

        public void LoadCustomServices()
        {
            var pluginDir = new DirectoryInfo("./plugins");
            foreach (var plugin in pluginDir.GetFiles())
            {
                // Console.WriteLine($"checking file for services {plugin.FullName}");
                var assembly = Assembly.LoadFile(plugin.FullName);
                foreach (Type customType in assembly.GetTypes().Where(customType => customType.GetInterfaces().Contains(typeof(ICustomService))))
                {
                    Console.WriteLine($"Added Service {customType.FullName}");
                    CustomServices.Add(customType);
                }

            }
        }
        public ServiceCollection AddPluginServices(ServiceCollection input)
        {
            foreach (Type customService in CustomServices)
            {
                input.AddSingleton(customService);
                Console.WriteLine($"1. {customService.Namespace} {customService.Name}");
            }
            return input;
        }
        public IServiceProvider CreateCustomServices(IServiceProvider sp)
        {
            foreach (Type customService in CustomServices)
            {
                var service = sp.GetService(customService);
                Console.WriteLine($"2. {customService.Namespace} {customService.Name}");
            }
            return sp;
        }



        public IServiceProvider BuildServiceProvider(ServiceCollection sc) => 
            sc.AddSingleton(_client)
            .AddSingleton(_commands)
            .AddSingleton<CommandHandler>()
            .BuildServiceProvider();

        public Task Log (LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;

        }
        private async Task MessageReceived(SocketMessage message)
        {
            Console.WriteLine(message.ToString());
            if (message.Content == "!ping")
            {
                await message.Channel.SendMessageAsync("Pong!");
            }
        }
    }
}
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
            // Console.WriteLine($"Checking file for modules {plugin.FullName}");
            var assembly = Assembly.LoadFile(plugin.FullName);
            var adding = _commands.AddModulesAsync(assembly);
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
