// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.DotNet.VersionTools.Util;

namespace Microsoft.DotNet.VersionTools.Dependencies.Repository
{
    public class RepositoryDependencyInfo : IDependencyInfo
    {
        public static RepositoryDependencyInfo CreateForSubmodule(
            string identity,
            string repository,
            string @ref,
            string path,
            bool remote)
        {
            if (remote)
            {
                return CreateRemote(repository, @ref, path);
            }

            // Get the current commit of the submodule as tracked by the containing repo. This
            // ensures local changes don't interfere.
            // https://git-scm.com/docs/git-submodule/1.8.2#git-submodule---cached
            string commit = GitCommand.SubmoduleStatusCached(path)
                .Substring(1, 40);

            return new RepositoryDependencyInfo(identity, repository, @ref, commit);
        }

        public static RepositoryDependencyInfo CreateRemote(
            string identity,
            string repository,
            string @ref,
            string gitWorkingDir = ".")
        {
            string remoteRefOutput = GitCommand.LsRemoteHeads(gitWorkingDir, repository, @ref);

            string[] remoteRefLines = remoteRefOutput
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (remoteRefLines.Length != 1)
            {
                string allRefs = "";
                if (remoteRefLines.Length > 1)
                {
                    allRefs = $" ({string.Join(", ", remoteRefLines)})";
                }

                throw new NotSupportedException(
                    $"The configured Ref '{@ref}'" +
                    $"must match exactly one ref on the remote, '{repository}'. " +
                    $"Matched {remoteRefLines.Length}{allRefs}. " +
                    $"(Working directory: '{gitWorkingDir}')");
            }

            string commit = remoteRefLines.Single().Split('\t').First();
            Trace.TraceInformation($"Found commit {commit} for '{repository}' at ref '{@ref}'.");
            return new RepositoryDependencyInfo(identity, repository, @ref, commit);
        }

        /// <summary>
        /// An identity that an updater can use to identify which dependency info to use. This can
        /// avoid having the updater know (and most likely duplicate) Repository and Ref.
        /// </summary>
        public string Identity { get; }

        /// <summary>
        /// The target repository, in a format that works with commands like "git fetch".
        /// 
        /// For example: https://github.com/dotnet/buildtools
        /// </summary>
        public string Repository { get; }

        /// <summary>
        /// The Git reference/ref (branch or tag) this dependency info tracks.
        /// </summary>
        public string Ref { get; }

        /// <summary>
        /// The commit that Ref points to in Repository.
        /// </summary>
        public string Commit { get; }

        public RepositoryDependencyInfo(
            string identity,
            string repository,
            string @ref,
            string commit)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }
            if (@ref == null)
            {
                throw new ArgumentNullException(nameof(@ref));
            }
            if (commit == null)
            {
                throw new ArgumentNullException(nameof(commit));
            }
            Identity = identity;
            Repository = repository;
            Ref = @ref;
            Commit = commit;
        }

        public override string ToString() => $"{SimpleName}:{Ref} ({Commit})";

        public string SimpleName => Repository.Split('/').Last();

        public string SimpleVersion => Commit?.Substring(0, Math.Min(7, Commit.Length)) ?? "latest";
    }
}
