using System.Threading.Tasks;

namespace Dotnetes.CommandLine.Tools
{
    internal class Kubectl: Tool
    {
        public static readonly Kubectl Default = new Kubectl(FindToolPath("kubectl"));

        public Kubectl(string toolPath) : base(toolPath) { }

        public Task<int> ApplyKustomizationAsync(string path) => ExecuteAsync("apply", "--kustomize", path);
    }
}
