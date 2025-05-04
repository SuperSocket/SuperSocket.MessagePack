using System;
using Xunit;
using SuperSocket.MessagePack;
using MessagePack;
using MessagePack.Resolvers;

namespace SuperSocket.MessagePack.Tests
{
    public class MessagePackTypeRegistryTests
    {
        [Fact]
        public void Constructor_WithDefaultOptions_CreatesInstance()
        {
            // Act
            var registry = new MessagePackTypeRegistry();
            
            // Assert
            Assert.NotNull(registry);
            Assert.NotNull(registry.Options);
        }

        [Fact]
        public void Constructor_WithCustomOptions_CreatesInstance()
        {
            // Arrange
            var options = MessagePackSerializerOptions.Standard
                .WithResolver(StandardResolver.Instance);
            
            // Act
            var registry = new MessagePackTypeRegistry(options);
            
            // Assert
            Assert.NotNull(registry);
            Assert.Same(options, registry.Options);
        }

        [Fact]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new MessagePackTypeRegistry(null));
            Assert.Equal("options", exception.ParamName);
        }

        [Fact]
        public void RegisterMessageType_ValidTypeAndId_RegistersSuccessfully()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            var messageType = typeof(TestMessage);
            int typeId = 1;
            
            // Act
            registry.RegisterMessageType(typeId, messageType);
            
            // Assert
            Assert.Equal(typeId, registry.GetTypeId(messageType));
            Assert.Equal(messageType, registry.GetMessageType(typeId));
        }

        [Fact]
        public void RegisterMessageType_NullType_ThrowsArgumentNullException()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => registry.RegisterMessageType(1, null));
            Assert.Equal("messageType", exception.ParamName);
        }

        [Fact]
        public void RegisterMessageType_DuplicateTypeId_ThrowsInvalidOperationException()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            registry.RegisterMessageType(1, typeof(TestMessage));
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(
                () => registry.RegisterMessageType(1, typeof(AnotherTestMessage)));
            
            Assert.Contains("Type ID 1 is already registered", exception.Message);
        }

        [Fact]
        public void RegisterMessageType_DuplicateType_ThrowsInvalidOperationException()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            registry.RegisterMessageType(1, typeof(TestMessage));
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(
                () => registry.RegisterMessageType(2, typeof(TestMessage)));
            
            Assert.Contains("already registered", exception.Message);
        }

        [Fact]
        public void GetTypeId_RegisteredType_ReturnsTypeId()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            var messageType = typeof(TestMessage);
            int typeId = 42;
            registry.RegisterMessageType(typeId, messageType);
            
            // Act
            int result = registry.GetTypeId(messageType);
            
            // Assert
            Assert.Equal(typeId, result);
        }

        [Fact]
        public void GetTypeId_UnregisteredType_ThrowsInvalidOperationException()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(
                () => registry.GetTypeId(typeof(TestMessage)));
            
            Assert.Contains("not registered", exception.Message);
        }

        [Fact]
        public void TryGetTypeId_RegisteredType_ReturnsTrue()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            var messageType = typeof(TestMessage);
            int typeId = 42;
            registry.RegisterMessageType(typeId, messageType);
            
            // Act
            bool result = registry.TryGetTypeId(messageType, out int retrievedTypeId);
            
            // Assert
            Assert.True(result);
            Assert.Equal(typeId, retrievedTypeId);
        }

        [Fact]
        public void TryGetTypeId_UnregisteredType_ReturnsFalse()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            
            // Act
            bool result = registry.TryGetTypeId(typeof(TestMessage), out int retrievedTypeId);
            
            // Assert
            Assert.False(result);
            Assert.Equal(0, retrievedTypeId);
        }

        [Fact]
        public void GetMessageType_RegisteredTypeId_ReturnsType()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            var messageType = typeof(TestMessage);
            int typeId = 42;
            registry.RegisterMessageType(typeId, messageType);
            
            // Act
            Type result = registry.GetMessageType(typeId);
            
            // Assert
            Assert.Equal(messageType, result);
        }

        [Fact]
        public void GetMessageType_UnregisteredTypeId_ThrowsInvalidOperationException()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(
                () => registry.GetMessageType(42));
            
            Assert.Contains("No message type registered for type ID 42", exception.Message);
        }

        [Fact]
        public void TryGetMessageType_RegisteredTypeId_ReturnsTrue()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            var messageType = typeof(TestMessage);
            int typeId = 42;
            registry.RegisterMessageType(typeId, messageType);
            
            // Act
            bool result = registry.TryGetMessageType(typeId, out Type retrievedType);
            
            // Assert
            Assert.True(result);
            Assert.Equal(messageType, retrievedType);
        }

        [Fact]
        public void TryGetMessageType_UnregisteredTypeId_ReturnsFalse()
        {
            // Arrange
            var registry = new MessagePackTypeRegistry();
            
            // Act
            bool result = registry.TryGetMessageType(42, out Type retrievedType);
            
            // Assert
            Assert.False(result);
            Assert.Null(retrievedType);
        }

        // Test message classes for testing
        [MessagePackObject]
        public class TestMessage 
        {
            [Key(0)]
            public string? Content { get; set; }
        }

        [MessagePackObject]
        public class AnotherTestMessage 
        {
            [Key(0)]
            public int Value { get; set; }
        }
    }
}