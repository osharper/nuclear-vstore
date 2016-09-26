using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Host.Controllers
{
    [Route("api/1.0/mgmt/bucket")]
    public sealed class BucketManagementController : Controller
    {
        private readonly IAmazonS3 _amazonS3;

        public BucketManagementController(IAmazonS3 amazonS3)
        {
            _amazonS3 = amazonS3;
        }

        [HttpGet]
        [Route("lifecycle-rules/{bucketName}")]
        public async Task<IEnumerable<LifecycleRule>> GetLifecycleRules(string bucketName)
        {
            var response = await _amazonS3.GetLifecycleConfigurationAsync(bucketName);
            return response.Configuration.Rules;
        }

        [HttpPut]
        [Route("expiration/{bucketName}")]
        public async Task PutExpirationRule(string bucketName)
        {
            var request = new PutLifecycleConfigurationRequest
                              {
                                  BucketName = bucketName,
                                  Configuration = new LifecycleConfiguration
                                                      {
                                                          Rules = new List<LifecycleRule>
                                                                      {
                                                                          new LifecycleRule
                                                                              {
                                                                                  Id = "Rule 1",
                                                                                  Status = LifecycleRuleStatus.Enabled,
                                                                                  Expiration = new LifecycleRuleExpiration
                                                                                                   {
                                                                                                       Days = 365
                                                                                                   }
                                                                              }
                                                                      }
                                                      }
                              };
            await _amazonS3.PutLifecycleConfigurationAsync(request);
        }
    }
}