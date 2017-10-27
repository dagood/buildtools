// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Dependencies.Repository;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public static class DependencyInfoUtils
    {
        /// <param name="ref">
        /// If null, match any Ref. Passing this is only needed if two Refs of a single Repository
        /// are both depended on.
        /// </param>
        public static RepositoryDependencyInfo FindRepositoryDependencyInfo(
            IEnumerable<IDependencyInfo> infos,
            string repository,
            string @ref)
        {
            RepositoryDependencyInfo[] matchingInfos = infos
                .OfType<RepositoryDependencyInfo>()
                .Where(info => info.Repository == repository)
                .Where(info => @ref == null || string.Equals(info.Ref, @ref, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matchingInfos.Length != 1)
            {
                string matchingInfoString = string.Join(", ", matchingInfos.AsEnumerable());
                int allSubmoduleInfoCount = infos.OfType<RepositoryDependencyInfo>().Count();

                throw new ArgumentException(
                    $"Expected exactly 1 {nameof(RepositoryDependencyInfo)} " +
                    $"matching repository '{repository}', ref '{@ref}', " +
                    $"but found {matchingInfos.Length}/{allSubmoduleInfoCount}: " +
                    $"'{matchingInfoString}'");
            }

            return matchingInfos[0];
        }
    }
}
