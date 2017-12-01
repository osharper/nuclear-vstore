using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CloningTool
{
    internal class CloneHelpers
    {
        internal static async Task ParallelRunAsync<T>(IEnumerable<T> list, int maxDegreeOfParallelism, Func<T, Task> callback)
        {
            var partitioner = Partitioner.Create(list);
            var partitions = partitioner.GetOrderablePartitions(maxDegreeOfParallelism)
                                        .Select(async partition =>
                                                    {
                                                        while (partition.MoveNext())
                                                        {
                                                            await callback(partition.Current.Value);
                                                        }
                                                    });
            await Task.WhenAll(partitions);
        }
    }
}
