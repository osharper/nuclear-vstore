using System;

using NuClear.VStore.Descriptors;

using Xunit;

namespace VStore.UnitTests
{
    public class DescriptorTests
    {
        [Fact]
        public void ShouldCheckEqualityOfIdentifyableObject()
        {
            var descriptorOne = new IdentifyableObjectDescriptor<long>(1L, DateTime.MinValue);
            var descriptorTwo = new IdentifyableObjectDescriptor<long>(descriptorOne.Id, DateTime.MaxValue);

            Assert.True(Equals(descriptorOne, descriptorTwo));
            Assert.True(Equals(descriptorTwo, descriptorOne));
            Assert.Equal(descriptorOne, descriptorOne);
            Assert.Equal(descriptorTwo, descriptorTwo);
            Assert.Equal(descriptorOne, descriptorTwo);
            Assert.Equal(descriptorTwo, descriptorOne);

            descriptorOne = new IdentifyableObjectDescriptor<long>(2L, DateTime.MinValue);
            Assert.NotEqual(descriptorOne, descriptorTwo);
            Assert.NotEqual(descriptorTwo, descriptorOne);
            Assert.False(Equals(descriptorOne, descriptorTwo));
            Assert.False(Equals(descriptorTwo, descriptorOne));
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