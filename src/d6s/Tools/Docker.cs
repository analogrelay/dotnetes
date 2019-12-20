using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dotnetes.CommandLine.Tools
{
    internal class Docker: Tool
    {
        public static readonly Docker Default = new Docker(FindToolPath("docker"));

        public Docker(string toolPath) : base(toolPath) { }

        public Task<int> BuildAsync(string path, string tag = null, string dockerfile = null, IEnumerable<KeyValuePair<string, string>> labels = null)
        {
            var args = new List<string>();
            args.Add("build");
            if(!string.IsNullOrEmpty(tag))
            {
                args.Add("--tag");
                args.Add(tag);
            }
            if(!string.IsNullOrEmpty(dockerfile))
            {
                args.Add("--file");
                args.Add(dockerfile);
            }
            if(labels != null)
            {
                foreach(var (name, value) in labels)
                {
                    args.Add("--label");
                    args.Add($"{name}=\"{value}\"");
                }
            }
            args.Add(path);
            return ExecuteAsync(args);
        }

        public Task<int> PushAsync(string tag) => ExecuteAsync("push", tag);
    }
}
