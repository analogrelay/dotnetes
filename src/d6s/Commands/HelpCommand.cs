using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using McMaster.Extensions.CommandLineUtils;

namespace Dotnetes.CommandLine.Commands
{
    [Command("help", Description = "Get help on a specific command, or list commands")]
    internal class HelpCommand
    {
        [Argument(0, "<CMD>", Description = "The command to get help on.")]
        public string Command { get; set; }

        public int OnExecute(CommandLineApplication cmd, IConsole console)
        {
            var app = cmd.Parent;
            if (string.IsNullOrEmpty(Command))
            {
                app.ShowHelp();
                return 0;
            }
            else
            {
                var command = app.Commands.FirstOrDefault(c => string.Equals(c.Name, Command, StringComparison.OrdinalIgnoreCase));
                if (command == null)
                {
                    console.Error.WriteLine($"Unknown command: {Command}");
                    return 1;
                }
                else
                {
                    command.ShowHelp();
                    return 0;
                }
            }
        }
    }
}
