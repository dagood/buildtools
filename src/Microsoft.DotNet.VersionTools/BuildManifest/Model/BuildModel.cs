// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.VersionTools.BuildManifest.Model
{
    public class BuildModel
    {
        public BuildModel(BuildIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            Identity = identity;
        }

        public BuildIdentity Identity { get; set; }

        public ArtifactSet Artifacts { get; set; } = new ArtifactSet();

        public List<EndpointModel> Endpoints { get; set; } = new List<EndpointModel>();

        public List<BuildModel> Builds { get; set; } = new List<BuildModel>();

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
            Identity.ToXmlAttributes(),
            Artifacts.ToXml(),
            Endpoints.Select(x => x.ToXml()),
            Builds.Select(x => x.ToXml()));

        public static BuildModel Parse(XElement xml) => new BuildModel(BuildIdentity.Parse(xml))
        {
            Artifacts = ArtifactSet.Parse(xml),
            Endpoints = xml.Elements("Endpoint").Select(EndpointModel.Parse).ToList(),
            Builds = xml.Elements("Build").Select(Parse).ToList()
        };
    }
}
