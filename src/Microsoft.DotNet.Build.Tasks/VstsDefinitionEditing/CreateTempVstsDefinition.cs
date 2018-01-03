// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.VstsBuildsApi;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Build.Tasks.VstsDefinitionEditing
{
    public class CreateTempVstsDefinition : BuildTask
    {
        [Required]
        public string DefinitionPath { get; set; }

        [Required]
        public string VstsUser { get; set; }

        [Required]
        public string VstsPat { get; set; }

        public string TempVstsFolder { get; set; } = "DotNet\\Drafts\\";

        public string ApiVersion { get; set; } = "3.2";

        public string UrlFormatString { get; set; } = "https://devdiv.visualstudio.com/DevDiv/_apps/hub/ms.vss-ciworkflow.build-ci-hub?_a=edit-build-definition&id={0}";

        public override bool Execute()
        {
            var config = VstsDefinitionClient.CreateDefaultConfig(
                Convert.ToBase64String(
                    Encoding.ASCII.GetBytes(
                        $"{VstsUser}:{VstsPat}")));
            config.BuildDefinitionEndpointConfig.ApiVersion = "3.2";

            var client = new VstsDefinitionClient(VstsUser, config);

            var def = JObject.Parse(File.ReadAllText(DefinitionPath));
            if (!string.IsNullOrEmpty(TempVstsFolder))
            {
                def["Path"] = TempVstsFolder;
            }

            using (var s = new MemoryStream(Encoding.UTF8.GetBytes(def.ToString())))
            {
                string createdId = client.CreateOrUpdateBuildDefinitionAsync(s).Result;
                string link = string.Format(UrlFormatString, createdId);
                Process.Start(link);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
