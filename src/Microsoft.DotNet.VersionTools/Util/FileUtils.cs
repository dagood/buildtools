// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.VersionTools.Util
{
    public static class FileUtils
    {
        /// <summary>
        /// Returns an Action that performs a replacement on a file, or null if the replacement
        /// makes no changes to the file's text. Attempts to preserve file encoding.
        /// </summary>
        /// <param name="path">Path of the file to change.</param>
        /// <param name="replacement">A function that takes file contents as input and returns the desired replacement.</param>
        public static Action GetUpdateFileContentsTask(
            string path,
            Func<string, string> replacement,
            Action initializeFile = null)
        {
            bool exists = File.Exists(path);
            string contents = string.Empty;
            Encoding encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            if (exists)
            {
                // Attempt to preserve the file's encoding, using a UTF-8 encoding with no BOM if
                // the file's encoding cannot be detected. 
                using (StreamReader reader = new StreamReader(
                    new FileStream(path, FileMode.Open, FileAccess.Read),
                    encoding,
                    detectEncodingFromByteOrderMarks: true))
                {
                    contents = reader.ReadToEnd();
                    encoding = reader.CurrentEncoding;
                }
            }
            else if (initializeFile == null)
            {
                throw new FileNotFoundException(
                    "File not found, but initializing it is not possible.",
                    path);
            }

            string newContents = replacement(contents);

            if (contents != newContents)
            {
                return () =>
                {
                    if (!exists)
                    {
                        initializeFile();
                    }
                    Trace.TraceInformation($"Writing changes to {path}");
                    File.WriteAllText(path, newContents, encoding);
                };
            }
            return null;
        }
    }
}
