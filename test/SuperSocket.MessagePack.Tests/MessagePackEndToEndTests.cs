using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SuperSocket.Client;
using SuperSocket.Connection;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Session;
using SuperSocket.Server.Host;
using Xunit;
using Xunit.Abstractions;

namespace SuperSocket.MessagePack.Tests
{
    public class MessagePackEndToEndTests
    {
        private readonly ITestOutputHelper _output;

        public MessagePackEndToEndTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [MessagePackObject]
        public class TestMessage
        {
            [Key(0)]
            public int Id { get; set; }

            [Key(1)]
            public string? Content { get; set; }
        }

        public class TestMessagePackageInfo
        {
            public object? Message { get; set; }
            public Type? MessageType { get; set; }
            public int TypeId { get; set; }
        }

        public class TestMessagePackPackageDecoder : MessagePackPackageDecoder<TestMessagePackageInfo>
        {
            public TestMessagePackPackageDecoder(MessagePackTypeRegistry typeRegistry) 
                : base(typeRegistry)
            {
            }

            protected override TestMessagePackageInfo CreatePackageInfo(object message, Type messageType, int typeId)
            {
                return new TestMessagePackageInfo
                {
                    Message = message,
                    MessageType = messageType,
                    TypeId = typeId
                };
            }
        }

        public class TestMessagePackPackageEncoder : MessagePackPackageEncoder<TestMessage>
        {
            public TestMessagePackPackageEncoder(MessagePackTypeRegistry typeRegistry) 
                : base(typeRegistry)
            {
            }
        }

        [Fact]
        public async Task SendReceiveMessagePackMessage_ShouldWorkCorrectly()
        {
            // Set up message registry
            var registry = new MessagePackTypeRegistry();
            registry.Register<TestMessage>(1);
            
            // Set up server
            using var server = await SetupServerAsync(registry);
            
            // Set up client
            var encoder = new TestMessagePackPackageEncoder(registry);
            var clientFilter = new MessagePackPipelineFilter<TestMessagePackageInfo>(new TestMessagePackPackageDecoder(registry));
            
            var client = new EasyClient<TestMessagePackageInfo>(clientFilter)
            {
                Security = new SecurityOptions { TargetHost = "localhost" }
            }.AsClient();

            // Connect to server
            var connected = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5000));
            Assert.True(connected);

            // Send a test message
            var message = new TestMessage
            {
                Id = 123,
                Content = "Hello, MessagePack!"
            };
            
            // Create channel context for encoding
            await client.SendAsync(encoder, message);
            
            // Wait for response and check it
            var response = await client.ReceiveAsync();

            Assert.NotNull(response);
            Assert.Equal(1, response.TypeId);
            
            var testMessage = response.Message as TestMessage;
            Assert.NotNull(testMessage);
            Assert.Equal(123, testMessage!.Id);
            Assert.Equal("Hello, MessagePack!", testMessage.Content);
            
            // Clean up
            await client.CloseAsync();
        }

        private async Task<IServer> SetupServerAsync(MessagePackTypeRegistry registry)
        {
            var server = SuperSocketHostBuilder.Create<TestMessagePackageInfo>()
                .UsePackageDecoder<TestMessagePackPackageDecoder>()
                .UsePipelineFilter<MessagePackPipelineFilter<TestMessagePackageInfo>>()
                .UseSessionHandler(OnSessionConnected, OnSessionClosed)
                .ConfigureSuperSocket(options =>
                {
                    options.Name = "MessagePack Test Server";
                    options.Listeners = new List<ListenOptions>
                    {
                        new ListenOptions
                        {
                            Ip = "127.0.0.1",
                            Port = 5000
                        }
                    };
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddSingleton<MessagePackTypeRegistry>(registry);
                })
                .BuildAsServer();

            await server.StartAsync();
            return server;
        }

        private ValueTask OnSessionConnected(IAppSession session)
        {
            _output.WriteLine($"Session connected: {session.SessionID}");
            return ValueTask.CompletedTask;
        }
        
        private ValueTask OnSessionClosed(IAppSession session, CloseEventArgs e)
        {
            _output.WriteLine($"Session closed: {session.SessionID}, Reason: {e.Reason}");
            return ValueTask.CompletedTask;
        }
    }

    // Extension methods for MessagePackTypeRegistry
    public static class MessagePackTypeRegistryExtensions
    {
        public static void Register<T>(this MessagePackTypeRegistry registry, int typeId)
        {
            registry.RegisterMessageType(typeId, typeof(T));
        }
    }
}
