using System;
using System.Buffers;
using System.Buffers.Binary;
using MessagePack;
using SuperSocket.ProtoBase;

namespace SuperSocket.MessagePack
{
    /// <summary>
    /// Provides encoding functionality for MessagePack messages, transforming them into network-ready binary packets.
    /// </summary>
    /// <remarks>
    /// The encoder prepends each message with an 8-byte header consisting of:
    /// - 4 bytes for message size (big-endian)
    /// - 4 bytes for message type ID (big-endian)
    /// </remarks>
    public abstract class MessagePackPackageEncoder<TPackageInfo> : IPackageEncoder<TPackageInfo>
    {
        private readonly MessagePackTypeRegistry _typeRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackPackageEncoder{TPackageInfo}"/> class.
        /// </summary>
        /// <param name="typeRegistry">The MessagePack type registry to use for encoding</param>
        /// <exception cref="ArgumentNullException">Thrown when typeRegistry is null</exception>
        public MessagePackPackageEncoder(MessagePackTypeRegistry typeRegistry)
        {
            _typeRegistry = typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));
        }

        /// <summary>
        /// Encodes a MessagePackPackageInfo into a binary format suitable for network transmission
        /// </summary>
        /// <param name="writer">The buffer writer to write the encoded package to</param>
        /// <param name="package">The MessagePack package to encode</param>
        /// <returns>The total number of bytes written</returns>
        /// <exception cref="ArgumentNullException">Thrown when package is null</exception>
        public int Encode(IBufferWriter<byte> writer, TPackageInfo package)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));

            var message = GetMessagePackObject(package);
            if (message == null)
                throw new ArgumentException("Failed to get MessagePack object from package", nameof(package));

            var messageType = message.GetType();
            int typeId = GetMessageTypeId(messageType);

            // Reserve space for the header (message size + type ID)
            var headerSegment = writer.GetSpan(8);
            writer.Advance(8);

            // Serialize the message directly to the writer
            var bytesWritten = MessagePackSerializer.Serialize(writer, message, messageType, _typeRegistry.Options);

            // Write the header
            BinaryPrimitives.WriteInt32BigEndian(headerSegment, bytesWritten); // Message size
            BinaryPrimitives.WriteInt32BigEndian(headerSegment.Slice(4), typeId); // Type ID

            // Return total bytes written (header + message)
            return bytesWritten + 8;
        }

        /// <summary>
        /// Gets the type ID for a message type from the registry.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <returns>The type ID.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the message type is not registered</exception>
        protected int GetMessageTypeId(Type messageType)
        {
            int typeId = 0;
            
            if (messageType == null)
                return typeId;

            // Try to get from registry if a registry is provided
            if (_typeRegistry != null)
            {
                if (!_typeRegistry.TryGetTypeId(messageType, out typeId))
                {
                    // If we're using a custom type ID, throw error when not found
                    if (GetProtobufMessageTypeId(default) != 0)
                        throw new InvalidOperationException($"Message type {messageType.FullName} is not registered in the type registry");
                }
            }

            return typeId;
        }

        /// <summary>
        /// Converts a package info into a MessagePack object.
        /// </summary>
        /// <param name="package">The package.</param>
        protected abstract object GetMessagePackObject(TPackageInfo package);

        /// <summary>
        /// Gets the MessagePack message type ID from the package.
        /// </summary>
        /// <param name="package">The package.</param>
        protected virtual int GetProtobufMessageTypeId(TPackageInfo package)
        {
            return 0;
        }
    }

    /// <summary>
    /// A concrete implementation of MessagePackPackageEncoder for objects.
    /// </summary>
    public class MessagePackPackageEncoder : MessagePackPackageEncoder<object>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackPackageEncoder"/> class.
        /// </summary>
        /// <param name="typeRegistry">The MessagePack type registry to use for encoding</param>
        public MessagePackPackageEncoder(MessagePackTypeRegistry typeRegistry) 
            : base(typeRegistry)
        {
        }

        /// <summary>
        /// Returns the object directly as it is already a MessagePack object.
        /// </summary>
        /// <param name="package">The package object.</param>
        /// <returns>The package object itself.</returns>
        protected override object GetMessagePackObject(object package)
        {
            return package;
        }
    }
}