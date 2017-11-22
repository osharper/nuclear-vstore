using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NuClear.VStore.Options
{
    public static class DistributedLockOptionsExtensions
    {
        public static IReadOnlyCollection<(string Host, int Port)> GetEndPoints(this DistributedLockOptions options)
        {
            return options.EndPoints
                          .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                          .Aggregate(
                              new List<(string, int)>(),
                              (result, next) =>
                                  {
                                      var endpoint = next.Trim().Split(':');
                                      var host = endpoint[0].Trim();
                                      var port = int.Parse(endpoint[1].Trim());

                                      result.Add((host, port));

                                      return result;
                                  });
        }
    }
}