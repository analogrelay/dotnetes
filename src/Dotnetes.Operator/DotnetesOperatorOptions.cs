using System;

namespace Dotnetes.Operator
{
    internal class DotnetesOperatorOptions
    {
        /// <summary>
        /// Gets or sets the interval at which the operator will check for new activity.
        /// </summary>
        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(1);
    }
}
