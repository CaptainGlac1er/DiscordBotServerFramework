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
        public Program(dynamic config)
        {
            Token = config.DiscordToken;
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
            new Program(config).MainAsync(config).GetAwaiter().GetResult();
        }

        private readonly CommandService _commands;
        private readonly InteractionService _interactions;
        private readonly DiscordSocketClient _client;

        private Dictionary<Type, Type> CustomServices = new Dictionary<Type, Type>();

        private ServiceCollection _serviceCollection;

        private IServiceProvider _serviceProvider;

        public async Task MainAsync(dynamic config)
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
            var pluginDir = new DirectoryInfo("./plugins");
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
        }
        public ServiceCollection AddPluginServices(ServiceCollection input)
        {
            foreach (Type customService in CustomServices.Values)
            {
                Console.WriteLine($"1. Adding Service {customService}");
                input.AddSingleton(customService);
                Console.WriteLine($"1. Finished Adding Service {customService.Namespace} {customService.Name}");
            }
            return input;
        }
        public IServiceProvider CreateCustomServices(IServiceProvider sp)
        {
            foreach (Type customService in CustomServices.Values)
            {
                Console.WriteLine($"2. Creating Service {customService}");
                var service = sp.GetService(customService);
                Console.WriteLine($"2. Finished Creating Service {customService.Namespace} {customService.Name}");
            }
            return sp;
        }



        public IServiceProvider BuildServiceProvider(ServiceCollection sc) => 
            sc.AddSingleton(_client)
            .AddSingleton(_commands)
            .AddSingleton(_interactions)
            .AddSingleton<CommandHandler>()
            .AddSingleton<InteractionHandler>()
            .AddSingleton<LongTermFileService>(di => new LongTermFileService("data"))
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
