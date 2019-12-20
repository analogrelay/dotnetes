using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dotnetes.Operator
{
    internal class KubernetesOptions
    {
        /// <summary>
        /// Gets or sets a boolean indicating the cluster authentication mode.
        /// </summary>
        public ClusterAuthenticationMode ClusterAuthentication { get; set; } = ClusterAuthenticationMode.InCluster;

        /// <summary>
        /// Gets or sets the path to the config file (if not specified, uses the default location).
        /// </summary>
        public string ConfigFilePath { get; set; }
    }

    internal enum ClusterAuthenticationMode
    {
        LocalConfigFile,
        InCluster
    }
}
