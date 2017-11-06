using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using RedLockNet.SERedis.Configuration;

namespace NuClear.VStore.Options
{
    public static class DistributedLockOptionsExtensions
    {
        public static IEnumerable<(string Host, string IpAddress, int Port)> GetEndPoints(this DistributedLockOptions options)
        {
            return options.EndPoints
                          .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                          .Aggregate(
                              new List<(string, string, int)>(),
                              (result, next) =>
                                  {
                                      var endpoint = next.Trim().Split(':');
                                      var host = endpoint[0].Trim();
                                      var port = int.Parse(endpoint[1].Trim());

                                      var addresses = Dns.GetHostAddresses(host);
                                      var ipAddress = addresses[0].ToString();
                                      result.Add((host, ipAddress, port));

                                      return result;
                                  });
        }
    }
}