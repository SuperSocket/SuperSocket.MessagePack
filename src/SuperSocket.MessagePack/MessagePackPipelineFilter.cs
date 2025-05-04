using System;
using System.Buffers;
using System.Buffers.Binary;
using SuperSocket.ProtoBase;

namespace SuperSocket.MessagePack
{
    /// <summary>
    /// Pipeline filter for handling MessagePack messages with fixed-length headers.
    /// </summary>
    /// <remarks>
    /// The filter expects an 8-byte header:
    /// - First 4 bytes: Message size in big-endian format
    /// - Next 4 bytes: Message type ID in big-endian format
    /// </remarks>
    public class MessagePackPipelineFilter<TPackageInfo> : FixedHeaderPipelineFilter<TPackageInfo>
        where TPackageInfo : class
    {
        private readonly IPackageDecoder<TPackageInfo> _decoder;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackPipelineFilter{TPackageInfo}"/> class.
        /// </summary>
        /// <param name="decoder">The decoder to use for converting binary data to TPackageInfo instances</param>
        /// <exception cref="ArgumentNullException">Thrown when decoder is null</exception>
        public MessagePackPipelineFilter(IPackageDecoder<TPackageInfo> decoder)
            : base(8) // 8-byte header (4 bytes for size + 4 bytes for type ID)
        {
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <summary>
        /// Gets the body length from the header.
        /// </summary>
        /// <param name="buffer">The buffer containing the header</param>
        /// <returns>The length of the message body</returns>
        protected override int GetBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
        {
            var reader = new SequenceReader<byte>(buffer);
            reader.TryReadBigEndian(out int bodyLength);
            return bodyLength;
        }

        /// <summary>
        /// Decodes the package from the buffer.
        /// </summary>
        /// <param name="buffer">The buffer containing the package data</param>
        /// <returns>The decoded package</returns>
        protected override TPackageInfo DecodePackage(ref ReadOnlySequence<byte> buffer)
        {
            return _decoder.Decode(ref buffer, null);
        }
    }
}