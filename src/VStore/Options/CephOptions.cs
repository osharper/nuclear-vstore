﻿namespace NuClear.VStore.Options
{
    public sealed class CephOptions
    {
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }
        public int DegreeOfParallelism { get; set; }
        public string TemplatesBucketName { get; set; }
        public string ObjectsBucketName { get; set; }
        public string FilesBucketName { get; set; }
    }
}