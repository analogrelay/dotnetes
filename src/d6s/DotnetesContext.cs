namespace Dotnetes.CommandLine
{
    internal class DotnetesContext
    {
        // TODO: Read this from some C O O L config.
        public static readonly DotnetesContext Default = new DotnetesContext(
            name: "anursedotnetes",
            @namespace: "default",
            dnsName: "anurse-dotnetes.westus2.cloudapp.azure.com",
            containerRegistry: "anursedotnetes.azurecr.io");

        public DotnetesContext(string name, string @namespace, string dnsName, string containerRegistry)
        {
            Name = name;
            Namespace = @namespace;
            DnsName = dnsName;
            ContainerRegistry = containerRegistry;
        }

        public string Name { get; }
        public string Namespace { get; }
        public string DnsName { get; }
        public string ContainerRegistry { get; }
    }
}
