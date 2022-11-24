using System;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;

public class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interaction;
    private readonly IServiceProvider _services;

    public InteractionHandler(IServiceProvider services, InteractionService interactions, DiscordSocketClient client)
    {
        _interaction = interactions;
        _services = services;
        _client = client;
    }

    public void InitializeAsync()
    {
        _client.InteractionCreated += HandleInteractionAsync;
        _client.AutocompleteExecuted += HandleInteractionAsync;
    }

    public async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        Console.WriteLine(interaction.ToString());
        if (!(interaction is SocketInteraction component)) return;
        Console.WriteLine($"'{component.Data}' read as a interaction");
        var result = await _interaction.ExecuteCommandAsync(
            new SocketInteractionContext(_client, component),
            _services
        );
    }

    public async Task InitCommands()
    {
        foreach (var guild in _client.Guilds)
            try
            {
                await _interaction.RegisterCommandsToGuildAsync(guild.Id);
            }
            catch (InvalidOperationException exception)
            {
                Console.WriteLine(exception.Message);
            }
    }
}