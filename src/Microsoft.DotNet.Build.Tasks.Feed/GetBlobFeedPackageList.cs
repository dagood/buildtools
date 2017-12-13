// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class GetBlobFeedPackageList : BlobFeedTask
    {
        private const string NuGetPackageInfoId = "PackageId";
        private const string NuGetPackageInfoVersion = "PackageVersion";

        [Output]
        public ITaskItem[] PackageInfos { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        protected override async Task<bool> ExecuteAsync()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Listing blob feed packages...");

                BlobFeedAction action = new BlobFeedAction(ExpectedFeedUrl, AccountKey, Log);

                ISet<PackageIdentity> packages = await action.GetPackageIdentitiesAsync();

                PackageInfos = packages.Select(ConvertToPackageInfoItem).ToArray();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        private ITaskItem ConvertToPackageInfoItem(PackageIdentity identity)
        {
            var metadata = new Dictionary<string, string>
            {
                [NuGetPackageInfoId] = identity.Id,
                [NuGetPackageInfoVersion] = identity.Version.ToString()
            };

            return new TaskItem(identity.ToString(), metadata);
        }
    }
}
