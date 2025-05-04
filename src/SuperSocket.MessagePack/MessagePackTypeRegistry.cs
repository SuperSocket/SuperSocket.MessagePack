using System;
using System.Collections.Generic;
using MessagePack;
using SuperSocket.ProtoBase;

namespace SuperSocket.MessagePack
{
    /// <summary>
    /// A centralized registry for MessagePack message types and their type identifiers.
    /// This class can be shared between encoders, decoders, clients, and servers.
    /// </summary>
    public class MessagePackTypeRegistry
    {
        private readonly Dictionary<int, Type> _idToTypes = new Dictionary<int, Type>();
        private readonly Dictionary<Type, int> _typeToIds = new Dictionary<Type, int>();
        private readonly MessagePackSerializerOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackTypeRegistry"/> class.
        /// </summary>
        public MessagePackTypeRegistry() : this(MessagePackSerializerOptions.Standard)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackTypeRegistry"/> class with custom options.
        /// </summary>
        /// <param name="options">The MessagePack serializer options to use</param>
        public MessagePackTypeRegistry(MessagePackSerializerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Register a message type with its type identifier
        /// </summary>
        /// <param name="typeId">The message type identifier</param>
        /// <param name="messageType">The message type</param>
        /// <exception cref="ArgumentNullException">Thrown when messageType is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when the type ID or message type is already registered</exception>
        public void RegisterMessageType(int typeId, Type messageType)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (_idToTypes.ContainsKey(typeId))
                throw new InvalidOperationException($"Type ID {typeId} is already registered");

            if (_typeToIds.ContainsKey(messageType))
                throw new InvalidOperationException($"Message type {messageType.FullName} is already registered");

            _idToTypes[typeId] = messageType;
            _typeToIds[messageType] = typeId;
        }

        /// <summary>
        /// Get the type identifier for a message type
        /// </summary>
        /// <param name="messageType">The message type</param>
        /// <returns>The type identifier</returns>
        /// <exception cref="InvalidOperationException">Thrown when the message type is not registered</exception>
        public int GetTypeId(Type messageType)
        {
            if (!_typeToIds.TryGetValue(messageType, out int typeId))
                throw new InvalidOperationException($"Message type {messageType.FullName} is not registered");

            return typeId;
        }

        /// <summary>
        /// Try to get the type identifier for a message type
        /// </summary>
        /// <param name="messageType">The message type</param>
        /// <param name="typeId">The type identifier (output)</param>
        /// <returns>True if the message type is registered; otherwise, false</returns>
        public bool TryGetTypeId(Type messageType, out int typeId)
        {
            return _typeToIds.TryGetValue(messageType, out typeId);
        }

        /// <summary>
        /// Get the message type for a type identifier
        /// </summary>
        /// <param name="typeId">The type identifier</param>
        /// <returns>The message type</returns>
        /// <exception cref="InvalidOperationException">Thrown when the type ID is not registered</exception>
        public Type GetMessageType(int typeId)
        {
            if (!_idToTypes.TryGetValue(typeId, out var messageType))
                throw new InvalidOperationException($"No message type registered for type ID {typeId}");

            return messageType;
        }

        /// <summary>
        /// Try to get the message type for a type identifier
        /// </summary>
        /// <param name="typeId">The type identifier</param>
        /// <param name="messageType">The message type (output)</param>
        /// <returns>True if the type ID is registered; otherwise, false</returns>
        public bool TryGetMessageType(int typeId, out Type messageType)
        {
            return _idToTypes.TryGetValue(typeId, out messageType);
        }

        /// <summary>
        /// Gets the MessagePack serializer options
        /// </summary>
        public MessagePackSerializerOptions Options => _options;
    }
}