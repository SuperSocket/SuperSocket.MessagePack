using System;
using System.Buffers;
using System.Buffers.Binary;
using Xunit;
using MessagePack;
using SuperSocket.MessagePack;
using SuperSocket.ProtoBase;
using Moq;

namespace SuperSocket.MessagePack.Tests
{
    public class MessagePackIntegrationTests
    {
        [MessagePackObject]
        public class TestMessage 
        {
            [Key(0)]
            public string? Content { get; set; }
            
            [Key(1)]
            public int Number { get; set; }
        }
        
        public class TestPackageInfo
        {
            public string? Content { get; set; }
            public int Number { get; set; }
            public Type? MessageType { get; set; }
            public int TypeId { get; set; }
        }
        
        private class TestPackageEncoder : MessagePackPackageEncoder<TestPackageInfo>
        {
            public TestPackageEncoder(MessagePackTypeRegistry typeRegistry) : base(typeRegistry)
            {
            }

            protected override object GetMessagePackObject(TestPackageInfo package)
            {
                return new TestMessage 
                { 
                    Content = package.Content,
                    Number = package.Number
                };
            }

            protected override int GetMessagePackMessageTypeId(TestPackageInfo package)
            {
                // Use the TypeId property from the package
                return package.TypeId;
            }
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
                        Number = testMessage.Number,
                        MessageType = messageType,
                        TypeId = typeId
                    };
                }
                
                return base.CreatePackageInfo(message, messageType, typeId);
            }
        }
        
        [Fact]
        public void EncoderDecoder_RoundTrip_PreservesData()
        {
            // Arrange
            var typeRegistry = new MessagePackTypeRegistry();
            int typeId = 42;
            typeRegistry.RegisterMessageType(typeId, typeof(TestMessage));
            
            var encoder = new TestPackageEncoder(typeRegistry);
            var decoder = new TestPackageDecoder(typeRegistry);
            
            // Create a test package
            var originalPackage = new TestPackageInfo 
            { 
                Content = "This is a test message",
                Number = 12345,
                TypeId = typeId
            };
            
            // Use a buffer writer to encode the package
            var writer = new ArrayBufferWriter<byte>();
            
            // Act - Step 1: Encode the package
            int bytesWritten = encoder.Encode(writer, originalPackage);
            byte[] encodedData = writer.WrittenSpan.ToArray();
            
            // Act - Step 2: Decode the package directly
            var sequence = new ReadOnlySequence<byte>(encodedData);
            var decodedPackage = decoder.Decode(ref sequence, null);
            
            // Assert
            Assert.NotNull(decodedPackage);
            Assert.Equal(originalPackage.Content, decodedPackage.Content);
            Assert.Equal(originalPackage.Number, decodedPackage.Number);
            Assert.Equal(typeof(TestMessage), decodedPackage.MessageType);
            Assert.Equal(typeId, decodedPackage.TypeId);
        }
        
        [Fact]
        public void EncoderDecoder_MultipleMessages_DecodesAllCorrectly()
        {
            // Arrange
            var typeRegistry = new MessagePackTypeRegistry();
            int typeId = 42;
            typeRegistry.RegisterMessageType(typeId, typeof(TestMessage));
            
            var encoder = new TestPackageEncoder(typeRegistry);
            var decoder = new TestPackageDecoder(typeRegistry);
            
            // Create two test packages
            var package1 = new TestPackageInfo 
            { 
                Content = "First message",
                Number = 111,
                TypeId = typeId
            };
            
            var package2 = new TestPackageInfo 
            { 
                Content = "Second message",
                Number = 222,
                TypeId = typeId
            };
            
            // Encode both packages
            var writer1 = new ArrayBufferWriter<byte>();
            var writer2 = new ArrayBufferWriter<byte>();
            encoder.Encode(writer1, package1);
            encoder.Encode(writer2, package2);
            
            byte[] encodedData1 = writer1.WrittenSpan.ToArray();
            byte[] encodedData2 = writer2.WrittenSpan.ToArray();
            
            // Act & Assert - Decode first message
            var sequence1 = new ReadOnlySequence<byte>(encodedData1);
            var decodedPackage1 = decoder.Decode(ref sequence1, null);
            
            Assert.NotNull(decodedPackage1);
            Assert.Equal(package1.Content, decodedPackage1.Content);
            Assert.Equal(package1.Number, decodedPackage1.Number);
            
            // Act & Assert - Decode second message
            var sequence2 = new ReadOnlySequence<byte>(encodedData2);
            var decodedPackage2 = decoder.Decode(ref sequence2, null);
            
            Assert.NotNull(decodedPackage2);
            Assert.Equal(package2.Content, decodedPackage2.Content);
            Assert.Equal(package2.Number, decodedPackage2.Number);
        }
        
        [Fact]
        public void DecoderFunction_WhenCalledThroughFilter_WorksCorrectly()
        {
            // Arrange
            var typeRegistry = new MessagePackTypeRegistry();
            int typeId = 42;
            typeRegistry.RegisterMessageType(typeId, typeof(TestMessage));
            
            var mockDecoder = new Mock<IPackageDecoder<TestPackageInfo>>();
            var expectedPackage = new TestPackageInfo { Content = "Test message", Number = 123 };
            
            mockDecoder
                .Setup(d => d.Decode(ref It.Ref<ReadOnlySequence<byte>>.IsAny, It.IsAny<object>()))
                .Returns(expectedPackage);
                
            var filter = new MessagePackPipelineFilter<TestPackageInfo>(mockDecoder.Object);
            
            // Create a message with header + body
            byte[] messageData = new byte[20]; // 8 bytes header + 12 bytes body
            BinaryPrimitives.WriteInt32BigEndian(messageData, 12); // Message body length
            BinaryPrimitives.WriteInt32BigEndian(messageData.AsSpan().Slice(4), typeId);
            
            // Get the DecodePackage method via reflection
            var decodeMethod = typeof(MessagePackPipelineFilter<TestPackageInfo>)
                .GetMethod("DecodePackage", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                
            var sequence = new ReadOnlySequence<byte>(messageData);
            
            // Act - Call the DecodePackage method through reflection
            var parameters = new object[] { sequence };
            var result = decodeMethod!.Invoke(filter, parameters);
            
            // Assert
            Assert.Same(expectedPackage, result);
            mockDecoder.Verify(d => d.Decode(ref It.Ref<ReadOnlySequence<byte>>.IsAny, It.IsAny<object>()), Times.Once);
        }
    }
}