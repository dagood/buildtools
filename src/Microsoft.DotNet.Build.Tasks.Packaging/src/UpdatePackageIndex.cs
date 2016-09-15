﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.Packaging;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class UpdatePackageIndex : PackagingTask
    {
        private HashSet<string> _packageIdsToInclude;

        /// <summary>
        /// File to update or create
        /// </summary>
        [Required]
        public ITaskItem PackageIndexFile { get; set; }

        /// <summary>
        /// Specific packages to index
        /// </summary>
        public ITaskItem[] Packages { get; set; }

        /// <summary>
        /// Baseline packages to add
        ///   Identity: Package ID
        ///   Version: Package version
        /// </summary>
        public ITaskItem[] BaselinePackages { get; set; }

        /// <summary>
        /// Stable packages to add
        ///   Identity: Package ID
        ///   Version: Package version
        /// </summary>
        public ITaskItem[] StablePackages { get; set; }

        /// <summary>
        /// Module to package mappings to add
        ///   Identity: Module name without extension
        ///   Package: Package id which provides module
        /// </summary>
        public ITaskItem[] ModuleToPackages { get; set; }

        /// <summary>
        /// When used with PackageFolders restricts the set of packages indexed.
        /// </summary>
        public ITaskItem[] PackageIds { get; set; }

        /// <summary>
        /// Folders to index, can contain flat set of packages or expanded package format.
        /// </summary>
        public ITaskItem[] PackageFolders { get; set; }

        /// <summary>
        /// Pre-release version to use for all pre-release packages covered by this index.
        /// </summary>
        public string PreRelease { get; set; }

        public override bool Execute()
        {
            string indexFilePath = PackageIndexFile.GetMetadata("FullPath");

            PackageIndex index = File.Exists(indexFilePath) ?
                index = PackageIndex.Load(indexFilePath) :
                new PackageIndex();

            if (PackageIds != null && PackageIds.Any())
            {
                _packageIdsToInclude = new HashSet<string>(PackageIds.Select(i => i.ItemSpec), StringComparer.OrdinalIgnoreCase);
            }

            foreach(var package in Packages.NullAsEmpty().Select(f => f.GetMetadata("FullPath")))
            {
                Log.LogMessage($"Updating from {package}.");
                UpdateFromPackage(index, package);
            }

            foreach(var packageFolder in PackageFolders.NullAsEmpty().Select(f => f.GetMetadata("FullPath")))
            {
                var nupkgs = Directory.EnumerateFiles(packageFolder, "*.nupkg", SearchOption.TopDirectoryOnly);

                if (nupkgs.Any())
                {
                    foreach(var nupkg in nupkgs)
                    {
                        Log.LogMessage($"Updating from {nupkg}.");
                        UpdateFromPackage(index, nupkg, true);
                    }
                }
                else
                {
                    var nuspecFolders = Directory.EnumerateFiles(packageFolder, "*.nuspec", SearchOption.AllDirectories)
                        .Select(nuspec => Path.GetDirectoryName(nuspec));

                    foreach (var nuspecFolder in nuspecFolders)
                    {
                        Log.LogMessage($"Updating from {nuspecFolder}.");
                        UpdateFromFolderLayout(index, nuspecFolder, true);
                    }
                }
            }

            if (BaselinePackages != null)
            {
                foreach (var baselinePackage in BaselinePackages)
                {
                    var info = GetOrCreatePackageInfo(index, baselinePackage.ItemSpec);
                    var version = baselinePackage.GetMetadata("Version");

                    info.BaselineVersion = Version.Parse(version);
                }
            }

            if (StablePackages != null)
            {
                foreach (var stablePackage in StablePackages)
                {
                    var info = GetOrCreatePackageInfo(index, stablePackage.ItemSpec);
                    var version = stablePackage.GetMetadata("Version");

                    info.StableVersions.Add(Version.Parse(version));
                }
            }

            if (ModuleToPackages != null)
            {
                foreach (var moduleToPackage in ModuleToPackages)
                {
                    var package = moduleToPackage.GetMetadata("Package");
                    index.ModulesToPackages[moduleToPackage.ItemSpec] = package;
                }
            }

            if (!String.IsNullOrEmpty(PreRelease))
            {
                index.PreRelease = PreRelease;
            }

            index.Save(indexFilePath);

            return !Log.HasLoggedErrors;
        }

        private void UpdateFromFolderLayout(PackageIndex index, string path, bool filter = false)
        {
            var version = NuGetVersion.Parse(Path.GetFileName(path));
            var id = Path.GetFileName(Path.GetDirectoryName(path));

            if (filter && !ShouldInclude(id))
            {
                return;
            }

            var refFiles = Directory.EnumerateFiles(Path.Combine(path, "ref"), "*.dll", SearchOption.AllDirectories);

            if (!refFiles.Any())
            {
                refFiles = Directory.EnumerateFiles(Path.Combine(path, "lib"), "*.dll", SearchOption.AllDirectories);
            }

            var assemblyVersions = refFiles.Select(f => VersionUtility.GetAssemblyVersion(f));

            UpdateFromValues(index, id, version, assemblyVersions);
        }

        private void UpdateFromPackage(PackageIndex index, string packagePath, bool filter = false)
        {
            string id;
            NuGetVersion version;
            IEnumerable<Version> assemblyVersions;

            using (var reader = new PackageArchiveReader(packagePath))
            {
                var identity = reader.GetIdentity();
                id = identity.Id;
                version = identity.Version;

                if (filter && !ShouldInclude(id))
                {
                    return;
                }

                var refFiles = reader.GetFiles("ref").Where(r => !NuGetAssetResolver.IsPlaceholder(r));

                if (!refFiles.Any())
                {
                    refFiles = reader.GetFiles("lib");
                }

                assemblyVersions = refFiles.Select(refFile =>
                {
                    using (var refStream = reader.GetStream(refFile))
                    using (var memStream = new MemoryStream())
                    {
                        refStream.CopyTo(memStream);
                        memStream.Seek(0, SeekOrigin.Begin);
                        return VersionUtility.GetAssemblyVersion(memStream);
                    }
                }).ToArray();
            }

            UpdateFromValues(index, id, version, assemblyVersions);
        }

        private void UpdateFromValues(PackageIndex index, string id, NuGetVersion version, IEnumerable<Version> assemblyVersions)
        {
            PackageInfo info = GetOrCreatePackageInfo(index, id);

            var packageVersion = VersionUtility.As3PartVersion(version.Version);
            // if we have a stable version, add it to the stable versions list
            if (!version.IsPrerelease)
            {
                info.StableVersions.Add(packageVersion);
            }

            if (assemblyVersions != null)
            {
                var assmVersions = new HashSet<Version>(assemblyVersions.Where(v => v != null));

                foreach(var assemblyVersion in assmVersions)
                {
                    info.AddAssemblyVersionInPackage(assemblyVersion, packageVersion);
                }

                // remove any assembly mappings which claim to be in this package version, but aren't in the assemblyList
                var orphanedAssemblyVersions = info.AssemblyVersionInPackageVersion
                                                    .Where(pair => pair.Value == packageVersion && !assmVersions.Contains(pair.Key))
                                                    .Select(pair => pair.Key)
                                                    .ToArray();

                foreach(var orphanedAssemblyVersion in orphanedAssemblyVersions)
                {
                    info.AssemblyVersionInPackageVersion.Remove(orphanedAssemblyVersion);
                }
            }
        }

        private PackageInfo GetOrCreatePackageInfo(PackageIndex index, string id)
        {
            PackageInfo info;

            if (!index.Packages.TryGetValue(id, out info))
            {
                index.Packages[id] = info = new PackageInfo();
            }

            return info;
        }

        private bool ShouldInclude(string packageId)
        {
            return (_packageIdsToInclude != null) ? _packageIdsToInclude.Contains(packageId) : true;
        }
    }
}
