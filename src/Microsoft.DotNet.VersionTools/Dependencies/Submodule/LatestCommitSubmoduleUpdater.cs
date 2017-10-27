// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Dependencies.Repository;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.DotNet.VersionTools.Dependencies.Submodule
{
    /// <summary>
    /// Updates the submodule at the given path to the latest commit available from its target
    /// repository.
    /// </summary>
    public class LatestCommitSubmoduleUpdater : SubmoduleUpdater
    {
        public string Repository { get; }

        public string Ref { get; }

        public LatestCommitSubmoduleUpdater(string repository, string @ref)
        {
            if (string.IsNullOrEmpty(repository))
            {
                throw new ArgumentException(
                    "A repository must be specified. For example, 'origin'. Got null or empty string.",
                    nameof(repository));
            }
            Repository = repository;

            if (string.IsNullOrEmpty(@ref))
            {
                throw new ArgumentException(
                    "A ref must be specified. For example, 'master'. Got null or empty string.",
                    nameof(@ref));
            }
            Ref = @ref;
        }

        protected override string GetDesiredCommitHash(
            IEnumerable<IDependencyInfo> dependencyInfos,
            out IEnumerable<IDependencyInfo> usedDependencyInfos)
        {
            RepositoryDependencyInfo matchingInfo = DependencyInfoUtils
                .FindRepositoryDependencyInfo(dependencyInfos, Repository, Ref);

            usedDependencyInfos = new[] { matchingInfo };

            Trace.TraceInformation($"For {Path}, found: {matchingInfo}");

            return matchingInfo.Commit;
        }

        protected override void FetchRemoteBranch()
        {
            GitCommand.Fetch(Path, Repository, Ref);
        }
    }
}
