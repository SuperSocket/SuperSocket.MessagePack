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

#### Registering the Pipeline Filter (Recommended Approach)

The recommended way to register the MessagePackPipelineFilter is using dependency injection:

```csharp
// Create server builder
var server = SuperSocketHostBuilder.Create<YourPackageInfo>()
    // Register your custom package decoder
    .UsePackageDecoder<YourMessagePackDecoder>()
    // Register the pipeline filter by type
    .UsePipelineFilter<MessagePackPipelineFilter<YourPackageInfo>>()
    // Register required services
    .ConfigureServices((ctx, services) =>
    {
        // Register the type registry
        services.AddSingleton<MessagePackTypeRegistry>(registry);
        // Register other required services
    })
    .BuildAsServer();
```

This approach uses dependency injection to create and manage the pipeline filter, which makes testing easier and keeps your code more maintainable.

### MessagePackPackageEncoder

Provides encoding functionality for MessagePack messages, transforming them into network-ready binary packets with proper header information.

### MessagePackPackageDecoder

Provides decoding functionality for binary data into MessagePack objects based on the type identifier in the message header.

## Usage Example

### Server-side Setup

```csharp
// Create a type registry
var registry = new MessagePackTypeRegistry();

// Register your message types
registry.RegisterMessageType(1, typeof(LoginRequest));
registry.RegisterMessageType(2, typeof(LoginResponse));
// Add more message types as needed...

// Configure SuperSocket server
var server = SuperSocketHostBuilder.Create<YourPackageInfo>()
    // Register package decoder by type
    .UsePackageDecoder<YourMessagePackDecoder>()
    // Register pipeline filter by type
    .UsePipelineFilter<MessagePackPipelineFilter<YourPackageInfo>>()
    // Register session and package handlers
    .UseSessionHandler(OnSessionConnected, OnSessionClosed)
    .UsePackageHandler<YourPackageInfo>(async (session, package) =>
    {
        // Handle incoming messages
        var encoder = session.Server.ServiceProvider.GetRequiredService<IPackageEncoder<ResponseMessage>>();
        await session.SendAsync(encoder, new ResponseMessage()).ConfigureAwait(false);
    })
    // Configure server options
    .ConfigureSuperSocket(options =>
    {
        options.Name = "MessagePack Server";
        options.Listeners = new List<ListenOptions>
        {
            new ListenOptions
            {
                Ip = "127.0.0.1",
                Port = 5000
            }
        };
    })
    // Register services
    .ConfigureServices((ctx, services) =>
    {
        // Register type registry
        services.AddSingleton<MessagePackTypeRegistry>(registry);
        // Register message encoder
        services.AddSingleton<IPackageEncoder<ResponseMessage>, YourMessagePackEncoder>();
    })
    .BuildAsServer();

await server.StartAsync();
```

### Client-side Setup

```csharp
// Use the same type registry as the server
var registry = new MessagePackTypeRegistry();
registry.RegisterMessageType(1, typeof(LoginRequest));
registry.RegisterMessageType(2, typeof(LoginResponse));
// Add more message types as needed...

// Create encoder and decoder instances
var encoder = new YourMessagePackEncoder(registry);
var decoder = new YourMessagePackDecoder(registry);

// Create client filter and configure client
var clientFilter = new MessagePackPipelineFilter<YourPackageInfo>(decoder);
var client = new EasyClient<YourPackageInfo>(clientFilter)
{
    Security = new SecurityOptions { TargetHost = "localhost" }
}.AsClient();

// Connect to server
var connected = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5000));
if (connected)
{
    // Create and send a message
    var loginRequest = new LoginRequest { Username = "user", Password = "pwd" };
    await client.SendAsync(encoder, loginRequest);
    
    // Receive and process response
    var response = await client.ReceiveAsync();
    Console.WriteLine($"Received response of type: {response.TypeId}");
    
    // Close connection when done
    await client.CloseAsync();
}
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