﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.VersionTools.Upgrade
{
    /// <summary>
    /// A tool that uses buildInfos to perform an upgrade.
    /// </summary>
    public interface IDependencyUpgrader
    {
        /// <summary>
        /// Upgrades based on the given build infos and returns build infos used during upgrade.
        /// </summary>
        IEnumerable<BuildInfo> Upgrade(IEnumerable<BuildInfo> buildInfos);
    }
}
