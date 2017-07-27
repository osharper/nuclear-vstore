using System;

using NuClear.VStore.DataContract;
using NuClear.VStore.Descriptors;

using Xunit;

namespace VStore.UnitTests
{
    public class DescriptorTests
    {
        [Fact]
        public void ShouldCheckEqualityOfIdentifyableObject()
        {
            var recordOne = new IdentifyableObjectRecord<long>(1L, DateTime.MinValue);
            var recordTwo = new IdentifyableObjectRecord<long>(recordOne.Id, DateTime.MaxValue);

            Assert.True(Equals(recordOne, recordTwo));
            Assert.True(Equals(recordTwo, recordOne));
            Assert.Equal(recordOne, recordOne);
            Assert.Equal(recordTwo, recordTwo);
            Assert.Equal(recordOne, recordTwo);
            Assert.Equal(recordTwo, recordOne);

            recordOne = new IdentifyableObjectRecord<long>(2L, DateTime.MinValue);
            Assert.NotEqual(recordOne, recordTwo);
            Assert.NotEqual(recordTwo, recordOne);
            Assert.False(Equals(recordOne, recordTwo));
            Assert.False(Equals(recordTwo, recordOne));
        }

        [Fact]
        public void ShouldCheckEqualityOfVersionedObject()
        {
            var descriptorOne = new VersionedObjectDescriptor<long>(1L, null, DateTime.MinValue);
            var descriptorTwo = new VersionedObjectDescriptor<long>(descriptorOne.Id, descriptorOne.VersionId, DateTime.MaxValue);

            Assert.True(Equals(descriptorOne, descriptorTwo));
            Assert.True(Equals(descriptorTwo, descriptorOne));
            Assert.Equal(descriptorOne, descriptorOne);
            Assert.Equal(descriptorTwo, descriptorTwo);
            Assert.Equal(descriptorOne, descriptorTwo);
            Assert.Equal(descriptorTwo, descriptorOne);

            descriptorOne = new VersionedObjectDescriptor<long>(2L, "version", DateTime.MinValue);
            Assert.Equal(descriptorOne, descriptorOne);
            Assert.NotEqual(descriptorOne, descriptorTwo);
            Assert.NotEqual(descriptorTwo, descriptorOne);
            Assert.False(Equals(descriptorOne, descriptorTwo));
            Assert.False(Equals(descriptorTwo, descriptorOne));

            descriptorTwo = new VersionedObjectDescriptor<long>(descriptorOne.Id, descriptorOne.VersionId.ToUpper(), DateTime.MaxValue);
            Assert.True(Equals(descriptorOne, descriptorTwo));
            Assert.True(Equals(descriptorTwo, descriptorOne));
            Assert.Equal(descriptorTwo, descriptorTwo);
            Assert.Equal(descriptorOne, descriptorTwo);
            Assert.Equal(descriptorTwo, descriptorOne);
        }
    }
}