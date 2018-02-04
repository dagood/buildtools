// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.VersionTools.Util;

namespace Microsoft.DotNet.VersionTools.BuildManifest.Model
{
    public class BuildModel
    {
        private static readonly string[] AttributeOrder =
        {
            nameof(Name),
            nameof(BuildId),
            nameof(ProductVersion),
            nameof(Branch),
            nameof(Commit)
        };

        private static readonly string[] RequiredAttributes =
        {
            nameof(Name)
        };

        public IDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public string Name
        {
            get { return Attributes.GetOrDefault(nameof(Name)); }
            set { Attributes[nameof(Name)] = value; }
        }

        public string BuildId
        {
            get { return Attributes.GetOrDefault(nameof(BuildId)); }
            set { Attributes[nameof(BuildId)] = value; }
        }

        public string ProductVersion
        {
            get { return Attributes.GetOrDefault(nameof(ProductVersion)); }
            set { Attributes[nameof(ProductVersion)] = value; }
        }

        public string Branch
        {
            get { return Attributes.GetOrDefault(nameof(Branch)); }
            set { Attributes[nameof(Branch)] = value; }
        }

        public string Commit
        {
            get { return Attributes.GetOrDefault(nameof(Commit)); }
            set { Attributes[nameof(Commit)] = value; }
        }

        public ArtifactSet Artifacts { get; set; } = new ArtifactSet();

        public List<EndpointModel> Endpoints { get; set; } = new List<EndpointModel>();

        public List<BuildModel> Builds { get; set; } = new List<BuildModel>();

        public override string ToString()
        {
            string s = Name;
            if (!string.IsNullOrEmpty(ProductVersion))
            {
                s += $" {ProductVersion}";
            }
            if (!string.IsNullOrEmpty(Branch))
            {
                s += $" on '{Branch}'";
            }
            if (!string.IsNullOrEmpty(Commit))
            {
                s += $" ({Commit})";
            }
            if (!string.IsNullOrEmpty(BuildId))
            {
                s += $" build {BuildId}";
            }
            return s;
        }

        public void AddParticipantBuild(BuildModel build)
        {
            EndpointModel[] feeds = Endpoints.Where(e => e.IsOrchestratedBlobFeed).ToArray();
            if (feeds.Length != 1)
            {
                throw new InvalidOperationException(
                    $"1 orchestrated blob feed must exist, but found {feeds.Length}.");
            }
            EndpointModel feed = feeds[0];

            feed.Artifacts.Add(build.Artifacts);
            Builds.Add(build);
        }

        public XElement ToXml() => new XElement(
            "Build",
            Attributes
                .ThrowIfMissingAttributes(RequiredAttributes)
                .CreateXmlAttributes(AttributeOrder),
            Artifacts.ToXml(),
            Endpoints.Select(x => x.ToXml()),
            Builds.Select(x => x.ToXml()));

        public static BuildModel Parse(XElement xml) => new BuildModel
        {
            Attributes = xml
                .CreateAttributeDictionary()
                .ThrowIfMissingAttributes(RequiredAttributes),
            Artifacts = ArtifactSet.Parse(xml),
            Endpoints = xml.Elements("Endpoint").Select(EndpointModel.Parse).ToList(),
            Builds = xml.Elements("Build").Select(Parse).ToList()
        };
    }
}
