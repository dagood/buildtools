// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class PackageDeleteRequest
    {
        public string Id { get; }
        public string Version { get; }
        public string Reason { get; }

        public PackageDeleteRequest(string id, string version, string reason)
        {
            Id = id;
            Version = version;
            Reason = reason;
        }
    }
}
