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
using Discord.Interactions;

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
                MessageCacheSize = 0,
                GatewayIntents = GatewayIntents.AllUnprivileged
            });
            _commands = new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false, ThrowOnError = true });
            _interactions = new InteractionService(_client);
            _serviceCollection = new ServiceCollection();
        }
        private readonly string Token;
        static void Main(string[] args)
        {
            var configDef = new { DiscordToken = "" };
            var config = JsonConvert.DeserializeAnonymousType(File.ReadAllText(@"./config/discord.json"), configDef);
            new Program(config.DiscordToken).MainAsync().GetAwaiter().GetResult();
        }

        private readonly CommandService _commands;
        private readonly InteractionService _interactions;
        private readonly DiscordSocketClient _client;

        private List<Type> CustomServices = new List<Type>();

        private ServiceCollection _serviceCollection;

        private IServiceProvider _serviceProvider;

        public async Task MainAsync()
        {
            LoadCustomServices();
            _serviceCollection = AddPluginServices(_serviceCollection);
            _serviceProvider = BuildServiceProvider(_serviceCollection);
            _serviceProvider = CreateCustomServices(_serviceProvider);
            await _serviceProvider.GetRequiredService<CommandHandler>().InitializeAsync();
            await _serviceProvider.GetRequiredService<InteractionHandler>().InitializeAsync();

            _client.Log += Log;
            _client.MessageReceived += MessageReceived;
            _client.UserVoiceStateUpdated += _client_UserVoiceStateUpdated;
            _client.Ready += Ready;
            await _client.LoginAsync(TokenType.Bot, Token);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        public async Task Ready() {
            Console.WriteLine("Bot is ready");
            await _serviceProvider.GetRequiredService<InteractionHandler>().InitCommands();
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
                if(!plugin.FullName.Contains(".dll")) {
                    Console.WriteLine($"Did not load {plugin.FullName}");
                    continue;
                }
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
            .AddSingleton(_interactions)
            .AddSingleton<CommandHandler>()
            .AddSingleton<InteractionHandler>()
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
