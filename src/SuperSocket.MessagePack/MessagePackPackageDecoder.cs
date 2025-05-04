using System;
using System.Buffers;
using System.Buffers.Binary;
using MessagePack;
using SuperSocket.ProtoBase;

namespace SuperSocket.MessagePack
{
    /// <summary>
    /// Provides decoding functionality for binary data into MessagePack objects
    /// </summary>
    public abstract class MessagePackPackageDecoder<TPackageInfo> : IPackageDecoder<TPackageInfo>
        where TPackageInfo : class
    {
        private readonly MessagePackTypeRegistry _typeRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackPackageDecoder{TPackageInfo}"/> class.
        /// </summary>
        /// <param name="typeRegistry">The MessagePack type registry to use for decoding</param>
        /// <exception cref="ArgumentNullException">Thrown when typeRegistry is null</exception>
        public MessagePackPackageDecoder(MessagePackTypeRegistry typeRegistry)
        {
            _typeRegistry = typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));
        }

        /// <summary>
        /// Decodes a binary packet into a TPackageInfo instance.
        /// </summary>
        /// <param name="buffer">The buffer containing the binary packet</param>
        /// <param name="context">The context object (not used in this implementation)</param>
        /// <returns>The decoded package info</returns>
        /// <exception cref="ProtocolException">Thrown when the message cannot be decoded</exception>
        public TPackageInfo Decode(ref ReadOnlySequence<byte> buffer, object context)
        {
            var reader = new SequenceReader<byte>(buffer);
            
            // Skip the length field that was already processed by the filter
            reader.Advance(4);
            
            // Read the message type identifier
            reader.TryReadBigEndian(out int messageTypeId);

            if (!_typeRegistry.TryGetMessageType(messageTypeId, out var messageType))
                throw new ProtocolException($"No message type registered for type id: {messageTypeId}");

            // Use the remaining buffer (actual MessagePack data)
            var messageBuffer = buffer.Slice(8);
            
            try
            {
                // Deserialize the message using MessagePack
                var message = MessagePackSerializer.Deserialize(messageType, messageBuffer, _typeRegistry.Options);
                
                // Create and return the package info
                return CreatePackageInfo(message, messageType, messageTypeId);
            }
            catch (Exception ex)
            {
                throw new ProtocolException($"Failed to deserialize MessagePack data for type {messageType.FullName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a package info object from the decoded message
        /// </summary>
        /// <param name="message">The decoded message</param>
        /// <param name="messageType">The type of the message</param>
        /// <param name="typeId">The type identifier</param>
        protected virtual TPackageInfo CreatePackageInfo(object message, Type messageType, int typeId)
        {
            return message as TPackageInfo ?? throw new InvalidOperationException($"Cannot cast message of type {message.GetType()} to {typeof(TPackageInfo)}");
        }
    }
}