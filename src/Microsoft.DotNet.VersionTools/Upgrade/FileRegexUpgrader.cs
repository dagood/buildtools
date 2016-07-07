// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.VersionTools.Upgrade
{
    public abstract class FileRegexUpgrader : IDependencyUpgrader
    {
        protected List<BuildInfo> BuildInfosUsed = new List<BuildInfo>();

        IEnumerable<BuildInfo> IDependencyUpgrader.BuildInfosUsed => BuildInfosUsed;

        public string Path { get; set; }
        public Regex Regex { get; set; }
        public string VersionGroupName { get; set; }

        public void Upgrade(IEnumerable<BuildInfo> buildInfos)
        {
            ReplaceFileContents(
                Path,
                contents => ReplaceDependencyVersion(buildInfos, contents));
        }

        protected abstract string GetDesiredValue(IEnumerable<BuildInfo> buildInfos);

        private string ReplaceDependencyVersion(IEnumerable<BuildInfo> buildInfos, string contents)
        {
            string newValue = GetDesiredValue(buildInfos);

            if (newValue == null)
            {
                Trace.TraceError($"Could not find version information to change '{Path}' with '{Regex}'");
                return contents;
            }

            return ReplaceGroupValue(Regex, contents, VersionGroupName, newValue);
        }

        private static string ReplaceGroupValue(Regex regex, string input, string groupName, string newValue)
        {
            return regex.Replace(input, m =>
            {
                string replacedValue = m.Value;
                Group group = m.Groups[groupName];
                int startIndex = group.Index - m.Index;

                replacedValue = replacedValue.Remove(startIndex, group.Length);
                replacedValue = replacedValue.Insert(startIndex, newValue);

                return replacedValue;
            });
        }

        private static void ReplaceFileContents(string path, Func<string, string> replacement)
        {
            string contents = File.ReadAllText(path);

            contents = replacement(contents);

            File.WriteAllText(path, contents, Encoding.UTF8);
        }
    }
}
