using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dotnetes.CommandLine.Tools
{
    internal class DotNet: Tool
    {
        public static readonly DotNet Default = new DotNet(FindToolPath("dotnet"));

        public DotNet(string toolPath) : base(toolPath) { }

        public Task<int> PublishAsync(string projectPath, string runtimeIdentifier = null, string outputDirectory = null)
        {
            var args = new List<string>();
            args.Add("publish");

            if(!string.IsNullOrEmpty(runtimeIdentifier))
            {
                args.Add("--runtime");
                args.Add(runtimeIdentifier);
            }

            if(!string.IsNullOrEmpty(outputDirectory))
            {
                args.Add("--output");
                args.Add(outputDirectory);
            }
            args.Add(projectPath);

            return ExecuteAsync(args);
        }
    }
}
