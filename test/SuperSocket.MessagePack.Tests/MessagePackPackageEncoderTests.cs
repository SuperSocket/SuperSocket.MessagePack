using System;
using System.Buffers;
using System.Buffers.Binary;
using Xunit;
using SuperSocket.MessagePack;
using MessagePack;
using SuperSocket.ProtoBase;

namespace SuperSocket.MessagePack.Tests
{
    public class MessagePackPackageEncoderTests
    {
        private class TestPackageInfo
        {
            public string? Content { get; set; }
        }

        [MessagePackObject]
        public class TestMessage
        {
            [Key(0)]
            public string? Content { get; set; }
        }

        private class TestPackageEncoder : MessagePackPackageEncoder<TestPackageInfo>
        {
            public TestPackageEncoder(MessagePackTypeRegistry typeRegistry) : base(typeRegistry)
            {
            }

            protected override object GetMessagePackObject(TestPackageInfo package)
            {
                return new TestMessage { Content = package.Content };
            }

            protected override int GetMessagePackMessageTypeId(TestPackageInfo package)
            {
                // For testing purposes, we'll just return 0 to use the default type ID resolution
                return 0;
            }
        }

        private class TestPackageEncoderWithTypeId : MessagePackPackageEncoder<TestPackageInfo>
        {
            private readonly int _typeId;

            public TestPackageEncoderWithTypeId(MessagePackTypeRegistry typeRegistry, int typeId) : base(typeRegistry)
            {
                _typeId = typeId;
            }

            protected override object GetMessagePackObject(TestPackageInfo package)
            {
                return new TestMessage { Content = package.Content };
            }

            protected override int GetMessagePackMessageTypeId(TestPackageInfo package)
            {
                return _typeId;
            }
        }

        [Fact]
        public void Constructor_WithNullTypeRegistry_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new TestPackageEncoder(null!));
            Assert.Equal("typeRegistry", exception.ParamName);
        }

        [Fact]
        public void Encode_WithNullPackage_ThrowsArgumentNullException()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            registry.RegisterMessageType(1, typeof(TestMessage));
            var encoder = new TestPackageEncoder(registry);
            var writer = new ArrayBufferWriter<byte>();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => encoder.Encode(writer, null!));
            Assert.Equal("package", exception.ParamName);
        }

        [Fact]
        public void Encode_WithValidPackage_EncodesCorrectly()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            int typeId = 123;
            registry.RegisterMessageType(typeId, typeof(TestMessage));
            var encoder = new TestPackageEncoder(registry);
            var writer = new ArrayBufferWriter<byte>();
            var package = new TestPackageInfo { Content = "Hello, World!" };

            // Act
            int bytesWritten = encoder.Encode(writer, package);
            var writtenBytes = writer.WrittenSpan.ToArray();

            // Assert
            Assert.True(bytesWritten > 8); // Header (8 bytes) + message data
            
            // Verify header
            int messageSize = BinaryPrimitives.ReadInt32BigEndian(writtenBytes);
            int messageTypeId = BinaryPrimitives.ReadInt32BigEndian(writtenBytes.AsSpan().Slice(4));
            
            Assert.Equal(typeId, messageTypeId);
            Assert.Equal(writtenBytes.Length - 8, messageSize); // Total length - header size = message size
            
            // Deserialize to verify content
            var deserializedMessage = MessagePackSerializer.Deserialize<TestMessage>(
                new ReadOnlySequence<byte>(writtenBytes, 8, writtenBytes.Length - 8), 
                registry.Options);
            
            Assert.NotNull(deserializedMessage);
            Assert.Equal("Hello, World!", deserializedMessage.Content);
        }

        [Fact]
        public void Encode_WithExplicitTypeId_UsesProvidedTypeId()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            int explicitTypeId = 456;
            registry.RegisterMessageType(explicitTypeId, typeof(TestMessage));
            var encoder = new TestPackageEncoderWithTypeId(registry, explicitTypeId);
            var writer = new ArrayBufferWriter<byte>();
            var package = new TestPackageInfo { Content = "Hello with explicit type ID" };

            // Act
            int bytesWritten = encoder.Encode(writer, package);
            var writtenBytes = writer.WrittenSpan.ToArray();

            // Assert
            int messageTypeId = BinaryPrimitives.ReadInt32BigEndian(writtenBytes.AsSpan().Slice(4));
            Assert.Equal(explicitTypeId, messageTypeId);
        }

        [Fact]
        public void Encode_WithMessageTypeNotRegisteredInRegistry_ThrowsInvalidOperationException()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            // Deliberately NOT registering the TestMessage type
            var encoder = new TestPackageEncoder(registry);
            var writer = new ArrayBufferWriter<byte>();
            var package = new TestPackageInfo { Content = "This should fail" };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => encoder.Encode(writer, package));
            Assert.Contains("not registered", exception.Message);
        }
        
        [Fact]
        public void GetMessageTypeId_NullMessageType_ReturnsZero()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            var encoder = new TestPackageEncoder(registry);
            
            // Use reflection to access the protected method
            var result = TestHelper.InvokeNonPublicMethod<int>(
                encoder,
                "GetMessageTypeId",
                new object[] { null! }
            );
            
            // Assert
            Assert.Equal(0, result);
        }
    }
}