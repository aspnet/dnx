using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class DiagnosticMessageAggregator
    {
        public List<DiagnosticsListMessage> Aggregrate(IEnumerable<DiagnosticsListMessage> collections)
        {
            var raw = new List<Tuple<DiagnosticMessageView, FrameworkData>>();
            var processed = new List<Tuple<DiagnosticMessageView, FrameworkData>>();
            var frameworks = new HashSet<FrameworkData>();

            foreach (var collection in collections)
            {
                frameworks.Add(collection.Framework);
                foreach (var diagnostic in collection.Diagnostics)
                {
                    raw.Add(Tuple.Create(diagnostic, collection.Framework));
                }
            }

            foreach (var diagnostic in raw.ToLookup(d => d.Item1))
            {
                if (diagnostic.Count() == frameworks.Count)
                {
                    // if one diagnostic appears under all frameworks, then remove the duplications
                    processed.Add(Tuple.Create<DiagnosticMessageView, FrameworkData>(diagnostic.Key, null));
                }
                else
                {
                    foreach (var each in diagnostic)
                    {
                        processed.Add(each);
                    }
                }
            }

            return processed.GroupBy(tuple => tuple.Item2, tuple => tuple.Item1)
                            .Select(group => new DiagnosticsListMessage(group.ToList(), group.Key))
                            .ToList();
        }
    }
}
