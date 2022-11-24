using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using GlacierByte.Discord.Server.Api;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace DiscordBotServer;

internal class Program
{
    private readonly DiscordSocketClient _client;

    private readonly CommandService _commands;
    private readonly InteractionService _interactions;
    private readonly string _token;

    private readonly PluginService _customPluginService;

    public Program(dynamic config)
    {
        _token = config.DiscordToken;
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Verbose,
            AlwaysDownloadUsers = true,
            MessageCacheSize = 0,
            GatewayIntents = GatewayIntents.AllUnprivileged
        });
        _commands = new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false, ThrowOnError = true });
        _interactions = new InteractionService(_client);
        _customPluginService = new PluginService($"{AppDomain.CurrentDomain.BaseDirectory}plugins");
    }

    private static void Main(string[] args)
    {
        var configDef = new { DiscordToken = "" };
        var config = JsonConvert.DeserializeAnonymousType(File.ReadAllText(@"./config/discord.json"), configDef);
        new Program(config).MainAsync(config).GetAwaiter().GetResult();
    }

    public async Task MainAsync(dynamic config)
    {
        _client.Log += Log;
        _client.MessageReceived += MessageReceived;
        _client.UserVoiceStateUpdated += _client_UserVoiceStateUpdated;
        _client.Ready += Ready;
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
        await Task.Delay(-1);
    }

    public async Task Ready()
    {
        Console.WriteLine("Bot is ready");
        LoadCustomServices();
    }

    private Task _client_UserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
    {
        Console.WriteLine(arg1 + arg2.ToString() + arg3);
        return Task.CompletedTask;
    }

    public void LoadCustomServices()
    {
        _customPluginService.PluginUpdate += PluginsLoaded;
        _customPluginService.StartListeningToPluginFolder();
    }

    private async void PluginsLoaded(object sender, ServiceCollection sc)
    {
        var serviceProvider = BuildServiceProvider(sc);
        await _customPluginService.LoadDiscordCommands(serviceProvider, _commands);
        await _customPluginService.LoadDiscordInteractions(serviceProvider, _interactions);
        serviceProvider.GetRequiredService<CommandHandler>().InitializeAsync();
        serviceProvider.GetRequiredService<InteractionHandler>().InitializeAsync();

        await serviceProvider.GetRequiredService<InteractionHandler>().InitCommands();
    }


    public IServiceProvider BuildServiceProvider(ServiceCollection sc)
    {
        return sc.AddSingleton(_client)
            .AddSingleton(_commands)
            .AddSingleton(_interactions)
            .AddSingleton<CommandHandler>()
            .AddSingleton<InteractionHandler>()
            .AddSingleton(di => new LongTermFileService("data"))
            .BuildServiceProvider();
    }

    public Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private async Task MessageReceived(SocketMessage message)
    {
        Console.WriteLine(message.ToString());
        if (message.Content == "!ping") await message.Channel.SendMessageAsync("Pong!");
    }
}