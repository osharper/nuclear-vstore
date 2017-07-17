using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions
{
    public sealed class MultipartUploadSession
    {
        private readonly List<FilePart> _parts = new List<FilePart>();

        public MultipartUploadSession(Guid sessionId, SessionDescriptor sessionDescriptor, DateTime expiresAt, IElementDescriptor elementDescriptor, string fileKey, string fileName, string uploadId)
        {
            SessionId = sessionId;
            SessionDescriptor = sessionDescriptor;
            ElementDescriptor = elementDescriptor;
            FileKey = fileKey;
            FileName = fileName;
            UploadId = uploadId;
            SessionExpiresAt = expiresAt;
        }

        public Guid SessionId { get; }
        public SessionDescriptor SessionDescriptor { get; }
        public DateTime SessionExpiresAt { get; }
        public IElementDescriptor ElementDescriptor { get; }
        public string FileKey { get; }
        public string FileName { get; }
        public string UploadId { get; }
        public int NextPartNumber => _parts.Count + 1;
        public IReadOnlyCollection<FilePart> Parts => _parts;

        public bool IsCompleted { get; private set; }

        public void AddPart(string etag)
        {
            _parts.Add(new FilePart(NextPartNumber, etag));
        }

        public void Complete()
        {
            IsCompleted = true;
        }

        public sealed class FilePart
        {
            public FilePart(int partNumber, string etag)
            {
                PartNumber = partNumber;
                Etag = etag;
            }

            public int PartNumber { get; }
            public string Etag { get; }
        }
    }
}