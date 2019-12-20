namespace Dotnetes.CommandLine
{
    internal class DotnetesContext
    {
        // TODO: Read this from some C O O L config.
        public static readonly DotnetesContext Default = new DotnetesContext(
            name: "anursedotnetes",
            containerRegistry: "anursedotnetes.azurecr.io");

        public DotnetesContext(string name, string containerRegistry)
        {
            Name = name;
            ContainerRegistry = containerRegistry;
        }

        public string Name { get; }
        public string ContainerRegistry { get; }
    }
}
