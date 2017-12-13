// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public abstract class BlobFeedTask : MSBuild.Task
    {
        [Required]
        public string ExpectedFeedUrl { get; set; }

        [Required]
        public string AccountKey { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        protected abstract Task<bool> ExecuteAsync();

        protected BlobFeedAction CreateAction()
        {
            return new BlobFeedAction(ExpectedFeedUrl, AccountKey, Log);
        }
    }
}
