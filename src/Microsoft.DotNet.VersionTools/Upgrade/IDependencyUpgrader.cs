using System.Collections.Generic;

namespace Microsoft.DotNet.VersionTools.Upgrade
{
    /// <summary>
    /// A tool that uses buildInfos to perform an upgrade.
    /// </summary>
    public interface IDependencyUpgrader
    {
        void Upgrade(IEnumerable<BuildInfo> buildInfos);

        /// <summary>
        /// Which build infos were used during Upgrade. Used for informational purposes.
        /// </summary>
        IEnumerable<BuildInfo> BuildInfosUsed { get; }
    }
}
