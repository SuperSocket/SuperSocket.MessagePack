using System;
using System.Buffers;
using System.Buffers.Binary;
using Xunit;
using SuperSocket.MessagePack;
using MessagePack;
using SuperSocket.ProtoBase;

namespace SuperSocket.MessagePack.Tests
{
    public class MessagePackPackageDecoderTests
    {
        [MessagePackObject]
        public class TestMessage
        {
            [Key(0)]
            public string? Content { get; set; }
        }

        private class TestPackageInfo
        {
            public string? Content { get; set; }
            public Type? MessageType { get; set; }
            public int TypeId { get; set; }
        }

        private class TestPackageDecoder : MessagePackPackageDecoder<TestPackageInfo>
        {
            public TestPackageDecoder(MessagePackTypeRegistry typeRegistry) : base(typeRegistry)
            {
            }

            protected override TestPackageInfo CreatePackageInfo(object message, Type messageType, int typeId)
            {
                if (message is TestMessage testMessage)
                {
                    return new TestPackageInfo
                    {
                        Content = testMessage.Content,
                        MessageType = messageType,
                        TypeId = typeId
                    };
                }
                
                return base.CreatePackageInfo(message, messageType, typeId);
            }
        }

        [Fact]
        public void Constructor_WithNullTypeRegistry_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new TestPackageDecoder(null!));
            Assert.Equal("typeRegistry", exception.ParamName);
        }

        [Fact]
        public void Decode_WithValidMessagePackData_DecodesCorrectly()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            int typeId = 123;
            registry.RegisterMessageType(typeId, typeof(TestMessage));
            var decoder = new TestPackageDecoder(registry);
            
            // Create a test message
            var message = new TestMessage { Content = "Hello, World!" };
            
            // Serialize the message
            byte[] messageBytes = MessagePackSerializer.Serialize(message, registry.Options);
            
            // Create a buffer with the header (length + typeId) + serialized message
            var buffer = new byte[messageBytes.Length + 8];
            
            // Write header
            BinaryPrimitives.WriteInt32BigEndian(buffer, messageBytes.Length); // Message length
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan().Slice(4), typeId); // Type ID
            
            // Copy serialized message
            messageBytes.CopyTo(buffer, 8);
            
            // Create a read-only sequence from the buffer
            var sequence = new ReadOnlySequence<byte>(buffer);
            
            // Act
            var packageInfo = decoder.Decode(ref sequence, null);
            
            // Assert
            Assert.NotNull(packageInfo);
            Assert.Equal("Hello, World!", packageInfo.Content);
            Assert.Equal(typeof(TestMessage), packageInfo.MessageType);
            Assert.Equal(typeId, packageInfo.TypeId);
        }

        [Fact]
        public void Decode_WithUnregisteredTypeId_ThrowsProtocolException()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            // Deliberately NOT registering any type
            var decoder = new TestPackageDecoder(registry);
            
            // Create a buffer with an unregistered type ID
            var buffer = new byte[12]; // 4 for length + 4 for type ID + 4 for minimal message
            int unregisteredTypeId = 999;
            
            // Write header
            BinaryPrimitives.WriteInt32BigEndian(buffer, 4); // Message length
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan().Slice(4), unregisteredTypeId); // Type ID
            
            // Create a read-only sequence from the buffer
            var sequence = new ReadOnlySequence<byte>(buffer);
            
            // Act & Assert
            var exception = Assert.Throws<ProtocolException>(() => decoder.Decode(ref sequence, null));
            Assert.Contains($"No message type registered for type id: {unregisteredTypeId}", exception.Message);
        }

        [Fact]
        public void Decode_WithInvalidMessagePackData_ThrowsProtocolException()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            int typeId = 123;
            registry.RegisterMessageType(typeId, typeof(TestMessage));
            var decoder = new TestPackageDecoder(registry);
            
            // Create a buffer with invalid MessagePack data
            var buffer = new byte[12]; // 4 for length + 4 for type ID + 4 for invalid data
            
            // Write header
            BinaryPrimitives.WriteInt32BigEndian(buffer, 4); // Message length
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan().Slice(4), typeId); // Type ID
            
            // Fill the rest with invalid MessagePack data
            buffer[8] = 0xFF;
            buffer[9] = 0xFF;
            buffer[10] = 0xFF;
            buffer[11] = 0xFF;
            
            // Create a read-only sequence from the buffer
            var sequence = new ReadOnlySequence<byte>(buffer);
            
            // Act & Assert
            var exception = Assert.Throws<ProtocolException>(() => decoder.Decode(ref sequence, null));
            Assert.Contains("Failed to deserialize", exception.Message);
        }

        [Fact]
        public void CreatePackageInfo_WhenMessageCantBeCastToPackageInfo_ThrowsInvalidOperationException()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            var decoder = new TestPackageDecoder(registry);
            
            try
            {
                // Act
                TestHelper.InvokeNonPublicMethod<TestPackageInfo>(
                    decoder, 
                    "CreatePackageInfo",
                    new object[] { new object(), typeof(object), 1 }
                );
                
                // If we get here, no exception was thrown, which is a test failure
                Assert.True(false, "Expected exception was not thrown");
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                // Assert - check that the inner exception is of the expected type
                Assert.IsType<InvalidOperationException>(ex.InnerException);
                Assert.Contains("Cannot cast", ex.InnerException!.Message);
            }
        }
    }

    // Helper class to invoke non-public methods via reflection
    public static partial class TestHelper
    {
        public static T InvokeNonPublicMethod<T>(object obj, string methodName, object[] parameters)
        {
            var method = obj.GetType().GetMethod(
                methodName, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            return (T)(method?.Invoke(obj, parameters) ?? throw new InvalidOperationException($"Method {methodName} not found or returned null"));
        }
    }
}