# SuperSocket.MessagePack
[![build](https://github.com/SuperSocket/SuperSocket.MessagePack/actions/workflows/build.yaml/badge.svg)](https://github.com/SuperSocket/SuperSocket.MessagePack/actions/workflows/build.yaml)
[![NuGet Version](https://img.shields.io/nuget/v/SuperSocket.MessagePack.svg?style=flat)](https://www.nuget.org/packages/SuperSocket.MessagePack/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SuperSocket.MessagePack.svg?style=flat)](https://www.nuget.org/packages/SuperSocket.MessagePack/)

SuperSocket integration library providing efficient binary serialization and deserialization for network messages using MessagePack with cross-platform and cross-language compatibility.

## Features

- Efficient binary serialization/deserialization for SuperSocket using MessagePack
- Type-based message routing with centralized type registry
- Clean separation between encoding and decoding logic
- Support for multiple .NET target frameworks
- Cross-platform and cross-language compatibility

## Installation

```bash
dotnet add package SuperSocket.MessagePack
```

## Components

### MessagePackTypeRegistry

A centralized registry for MessagePack message types and their type identifiers. This registry can be shared between encoders, decoders, clients, and servers.

```csharp
// Create a type registry
var typeRegistry = new MessagePackTypeRegistry();

// Register message types with their type IDs
typeRegistry.RegisterMessageType(1, typeof(LoginRequest));
typeRegistry.RegisterMessageType(2, typeof(LoginResponse));
typeRegistry.RegisterMessageType(3, typeof(LogoutRequest));
```

### MessagePackPipelineFilter

Pipeline filter for handling MessagePack messages with fixed-length headers. The filter expects an 8-byte header consisting of:
- First 4 bytes: Message size in big-endian format
- Next 4 bytes: Message type ID in big-endian format

### MessagePackPackageEncoder

Provides encoding functionality for MessagePack messages, transforming them into network-ready binary packets with proper header information.

### MessagePackPackageDecoder

Provides decoding functionality for binary data into MessagePack objects based on the type identifier in the message header.

## Usage Example

### Server-side Setup

```csharp
// Create a type registry
var typeRegistry = new MessagePackTypeRegistry();

// Register your message types
typeRegistry.RegisterMessageType(1, typeof(LoginRequest));
typeRegistry.RegisterMessageType(2, typeof(LoginResponse));
// Add more message types as needed...

// Create your custom decoder and encoder
var decoder = new YourMessagePackDecoder(typeRegistry);
var encoder = new YourMessagePackEncoder(typeRegistry);

// Configure SuperSocket server
var server = SuperSocketHostBuilder.Create<YourPackageInfo>()
    .UsePipelineFilter(serviceProvider => new MessagePackPipelineFilter<YourPackageInfo>(decoder))
    .UsePackageEncoder(encoder)
    // Add other necessary configurations
    .Build();

await server.StartAsync();
```

### Client-side Setup

```csharp
// Use the same type registry with the same message type registrations as the server
var typeRegistry = new MessagePackTypeRegistry();
typeRegistry.RegisterMessageType(1, typeof(LoginRequest));
typeRegistry.RegisterMessageType(2, typeof(LoginResponse));
// Add more message types as needed...

// Create your custom client decoder and encoder
var decoder = new YourMessagePackDecoder(typeRegistry);
var encoder = new YourMessagePackEncoder(typeRegistry);

// Create and connect your client
var client = new EasyClient<YourPackageInfo>(new ClientPackageDecoder(decoder))
    .UsePackageEncoder(encoder);

await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 4040));

// Send a login request
await client.SendAsync(new LoginRequest { Username = "user", Password = "pwd" });
```

## Implementing Custom Encoders and Decoders

### Custom Encoder Example

```csharp
public class YourMessagePackEncoder : MessagePackPackageEncoder<YourPackageInfo>
{
    public YourMessagePackEncoder(MessagePackTypeRegistry typeRegistry) : base(typeRegistry)
    {
    }

    protected override object GetMessagePackObject(YourPackageInfo package)
    {
        // Extract the actual message object from your package
        return package.Message;
    }

    protected override int GetMessagePackMessageTypeId(YourPackageInfo package)
    {
        // Get the type ID from your package
        return package.TypeId;
    }
}
```

### Custom Decoder Example

```csharp
public class YourMessagePackDecoder : MessagePackPackageDecoder<YourPackageInfo>
{
    public YourMessagePackDecoder(MessagePackTypeRegistry typeRegistry) : base(typeRegistry)
    {
    }

    protected override YourPackageInfo CreatePackageInfo(object message, Type messageType, int typeId)
    {
        // Create your package instance from the decoded message
        return new YourPackageInfo
        {
            Message = message,
            MessageType = messageType,
            TypeId = typeId
        };
    }
}
```

## License

This project is licensed under the terms of the [LICENSE](LICENSE) file included in this repository.

## Related Projects

- [SuperSocket](https://github.com/kerryjiang/SuperSocket)
- [MessagePack for C#](https://github.com/MessagePack-CSharp/MessagePack-CSharp)