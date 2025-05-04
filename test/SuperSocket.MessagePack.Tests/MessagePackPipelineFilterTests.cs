using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Xunit;
using SuperSocket.MessagePack;
using SuperSocket.ProtoBase;
using Moq;

namespace SuperSocket.MessagePack.Tests
{
    public class MessagePackPipelineFilterTests
    {
        public class TestPackageInfo
        {
            public string? Content { get; set; }
        }

        [Fact]
        public void Constructor_WithNullDecoder_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new MessagePackPipelineFilter<TestPackageInfo>(null!));
            Assert.Equal("decoder", exception.ParamName);
        }

        [Fact]
        public void GetBodyLengthFromHeader_ReturnsCorrectLength()
        {
            // Arrange
            var mockDecoder = new Mock<IPackageDecoder<TestPackageInfo>>();
            var filter = new MessagePackPipelineFilter<TestPackageInfo>(mockDecoder.Object);
            
            // Create a test header with message size 123
            byte[] headerBytes = new byte[8];
            BinaryPrimitives.WriteInt32BigEndian(headerBytes, 123);
            BinaryPrimitives.WriteInt32BigEndian(headerBytes.AsSpan().Slice(4), 456); // Type ID (not needed for this test)
            
            var sequence = new ReadOnlySequence<byte>(headerBytes);
            
            // Use reflection to access the protected method
            int bodyLength = TestHelper.InvokeNonPublicMethod<int>(
                filter,
                "GetBodyLengthFromHeader",
                new object[] { sequence }
            );
            
            // Assert
            Assert.Equal(123, bodyLength);
        }

        [Fact]
        public void DecodePackage_CallsDecoder()
        {
            // Arrange
            var mockDecoder = new Mock<IPackageDecoder<TestPackageInfo>>();
            var expectedPackage = new TestPackageInfo { Content = "Test" };
            
            mockDecoder
                .Setup(d => d.Decode(ref It.Ref<ReadOnlySequence<byte>>.IsAny, It.IsAny<object>()))
                .Returns(expectedPackage);
            
            var filter = new MessagePackPipelineFilter<TestPackageInfo>(mockDecoder.Object);
            
            byte[] packageData = new byte[16]; // 8 bytes for header + 8 bytes for body
            var sequence = new ReadOnlySequence<byte>(packageData);
            
            // Use reflection to access the protected method
            var result = TestHelper.InvokeNonPublicMethodWithRefParam<TestPackageInfo>(
                filter, 
                "DecodePackage",
                sequence);
            
            // Assert
            Assert.Same(expectedPackage, result);
            mockDecoder.Verify(d => d.Decode(ref It.Ref<ReadOnlySequence<byte>>.IsAny, It.IsAny<object>()), Times.Once);
        }
    }

    // Simple implementation of a pipeline filter context for testing
    public class DefaultPipelineFilterContext
    {
        private readonly Memory<byte> _memory = new Memory<byte>(new byte[1024 * 64]);
        private int _length;

        public void AppendData(byte[] data)
        {
            data.CopyTo(_memory.Slice(_length).Span);
            _length += data.Length;
        }

        public void ConsumeBytes(int count)
        {
            if (count <= 0 || count > _length)
                throw new ArgumentOutOfRangeException(nameof(count));
            
            // Move remaining data to the beginning of the buffer
            if (count < _length)
            {
                _memory.Slice(count, _length - count).CopyTo(_memory);
            }
            
            _length -= count;
        }

        public ReadOnlySequence<byte> BufferSequence => new ReadOnlySequence<byte>(_memory.Slice(0, _length));
    }

    // Extended helper class to handle ref parameters
    public static partial class TestHelper
    {
        public static TResult InvokeNonPublicMethodWithRefParam<TResult>(object obj, string methodName, ReadOnlySequence<byte> sequence)
        {
            var method = obj.GetType().GetMethod(
                methodName, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Create a parameter array with one element for the ref parameter
            var parameters = new object[] { sequence };

            // Invoke the method
            var result = method?.Invoke(obj, parameters);

            return (TResult)(result ?? throw new InvalidOperationException($"Method {methodName} not found or returned null"));
        }
    }
}