using System;
using System.Diagnostics;
using System.Linq;
using Dotnetes.CommandLine.Commands;
using McMaster.Extensions.CommandLineUtils;

namespace Dotnetes.CommandLine
{
    [Command(FullName = "d6s", Description = "CLI for managing dotnetes instances.")]
    [Subcommand(typeof(PushCommand))]
    [Subcommand(typeof(HelpCommand))]
    class Program
    {
        static int Main(string[] args)
        {
#if DEBUG
            if (args.Any(a => a == "--debug"))
            {
                args = args.Where(a => a != "--debug").ToArray();
                Console.WriteLine($"Ready for debugger to attach. Process ID: {Process.GetCurrentProcess().Id}.");
                Console.WriteLine("Press ENTER to continue.");
                Console.ReadLine();
            }
#endif

            try
            {
                return CommandLineApplication.Execute<Program>(args);
            }
            catch (CommandLineException clex)
            {
                var oldFg = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(clex.Message);
                Console.ForegroundColor = oldFg;
                return 1;
            }
        }

        public void OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
        }
    }
}
