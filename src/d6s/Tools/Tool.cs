using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Dotnetes.CommandLine.Tools
{
    internal abstract class Tool
    {
        // ORDER MATTERS: ExeSuffixes is used by FindToolPath so must be above PowerShellPath
        private static readonly string[] ExeSuffixes = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
            new string[] { ".exe", ".cmd", ".ps1" } : new string[] { string.Empty };
        private static readonly string PowerShellPath = FindToolPath("powershell");

        private readonly string _filePath;
        private readonly string[] _baseArgs;

        protected Tool(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension) || string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
            {
                _filePath = filePath;
                _baseArgs = Array.Empty<string>();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase))
            {
                _filePath = Environment.GetEnvironmentVariable("COMSPEC");
                _baseArgs = new string[]
                {
                    "/c",
                    filePath,
                };
            }
            else if (string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                _filePath = PowerShellPath ?? throw new CommandLineException($"Could not find 'powershell', required to run command '{filePath}'");
                _baseArgs = new string[]
                {
                    "-File",
                    filePath,
                };
            }
            else
            {
                throw new CommandLineException($"Unable to launch unknown executable type: '{filePath}'");
            }
        }

        public static string FindToolPath(string toolName)
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            var pathElements = pathVar.Split(Path.PathSeparator);
            foreach (var candidate in pathElements)
            {
                foreach (var suffix in ExeSuffixes)
                {
                    var exe = Path.Combine(candidate, toolName + suffix);
                    if (File.Exists(exe))
                    {
                        return exe;
                    }
                }
            }
            return null;
        }

        public Task<int> ExecuteAsync(params string[] arguments) => ExecuteAsync((IEnumerable<string>)arguments);
        public Task<int> ExecuteAsync(IEnumerable<string> arguments)
        {
            var process = new Process();
            process.StartInfo.FileName = _filePath;
            foreach(var baseArg in _baseArgs)
            {
                process.StartInfo.ArgumentList.Add(baseArg);
            }
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
            process.EnableRaisingEvents = true;

            var tcs = new TaskCompletionSource<int>();

            process.Exited += (sender, e) =>
            {
                tcs.TrySetResult(process.ExitCode);
            };

            // TODO: Better logging
            var toolName = Path.GetFileNameWithoutExtension(_filePath);
            Console.WriteLine($"> {toolName} {string.Join(' ', arguments)}");
            // TODO: Verbosity!
            if (_baseArgs.Length > 0)
            {
                Console.WriteLine($">> {process.StartInfo.FileName} {string.Join(' ', process.StartInfo.ArgumentList)}");
            }
            process.Start();

            return tcs.Task;
        }
    }
}
