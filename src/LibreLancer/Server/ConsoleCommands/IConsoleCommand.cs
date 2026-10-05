namespace LibreLancer.Server.ConsoleCommands
{
    public interface IConsoleCommand
    {
        string Name { get; }
        string? Permission => null;
        void Run(Player player, string arguments);
    }
}
