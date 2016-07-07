// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public static class TraceListenerCollectionExtensions
    {
        public static MsBuildTraceListener[] AddMsBuildTraceListeners(
            this TraceListenerCollection listenerCollection,
            TaskLoggingHelper log)
        {
            var newListeners = new[]
            {
                TraceEventType.Error,
                TraceEventType.Warning,
                TraceEventType.Critical,
                TraceEventType.Information,
                TraceEventType.Verbose
            }.Select(t => new MsBuildTraceListener(log, t)).ToArray();

            listenerCollection.AddRange(newListeners);
            return newListeners;
        }

        public static void RemoveMsBuildTraceListeners(
            this TraceListenerCollection listenerCollection,
            IEnumerable<MsBuildTraceListener> traceListeners)
        {
            foreach (MsBuildTraceListener listener in traceListeners)
            {
                listenerCollection.Remove(listener);
            }
        }
    }
}