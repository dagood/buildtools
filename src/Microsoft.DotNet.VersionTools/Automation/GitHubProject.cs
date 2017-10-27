// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class GitHubProject
    {
        public static GitHubProject ParseUrl(string url)
        {
            var uri = new Uri(url);
            string host = uri.Host;
            if (host.StartsWith("www."))
            {
                host = host.Substring("www.".Length);
            }
            if (!string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Url {url} is not on the 'github.com' host.");
            }

            if (uri.Segments.Length < 3)
            {
                throw new ArgumentException($"Url {url} has {uri.Segments.Length} segments, minimum 3.");
            }

            string owner = uri.Segments[1].TrimEnd('/');
            string name = uri.Segments[2].TrimEnd('/');

            if (name.EndsWith(".git"))
            {
                name = name.Substring(0, name.Length - ".git".Length);
            }

            return new GitHubProject(name, owner);
        }

        public string Name { get; }
        public string Owner { get; }

        public GitHubProject(string name, string owner = null)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            Name = name;

            Owner = owner ?? "dotnet";
        }

        public string Segments => $"{Owner}/{Name}";
    }
}
