// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class DeleteFromBlobFeed : BlobFeedTask
    {
        [Required]
        public ITaskItem[] ItemsToDelete { get; set; }

        public bool Force { get; set; }

        public bool SkipCreateContainer { get; set; } = false;

        protected override async Task<bool> ExecuteAsync()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Performing feed push...");

                if (ItemsToDelete == null)
                {
                    Log.LogError($"No items to push. Please check ItemGroup ItemsToPush.");
                }
                else
                {
                    BlobFeedAction blobFeedAction = CreateAction();

                    if (!SkipCreateContainer)
                    {
                        await blobFeedAction.CreateContainerAsync(BuildEngine, false);
                    }

                    PackageDeleteRequest[] deleteItems = ItemsToDelete
                        .Select(item => new PackageDeleteRequest(
                            item.ItemSpec,
                            item.GetMetadata("PackageVersion"),
                            item.GetMetadata("DeleteReason")))
                        .ToArray();

                    await blobFeedAction.DeleteFromFeed(deleteItems, Force);
                }
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
