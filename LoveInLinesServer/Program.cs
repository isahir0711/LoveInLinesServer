namespace LoveInLinesServer;

using DTOs;
using Microsoft.AspNetCore.Builder;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public abstract class Program
{
    public static Task Main(string[] args)
    {
        return Task.Run(async () =>
        {

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            var frontUrl = builder.Configuration["frontURL"]!;


            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: "corsExample",
                    policy => { policy.WithOrigins(frontUrl).AllowAnyHeader().AllowAnyMethod(); });
            });

            WebApplication app = builder.Build();

            app.UseWebSockets();


            Dictionary<string, List<WebSocket>> rooms = new();

            app.Map("/ws", async context =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    WebSocket ws = await context.WebSockets.AcceptWebSocketAsync();

                    string roomId = context.Request.Query["roomId"];


                    if (!rooms.TryGetValue(roomId, out List<WebSocket> value))
                    {
                        value = [];
                        rooms[roomId] = value;
                    }

                    value.Add(ws);

                    Console.WriteLine($"{ws.State} Conectado en sala ==> {roomId}");
                    Console.WriteLine($"Numero de salas {rooms.Count}");
                    foreach (var item in value)
                    {
                        Console.WriteLine($"socket: {item}--");
                    }

                    async void HandleMessage(WebSocketReceiveResult result, byte[] buffer)
                    {
                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

                            ClientMessage message = JsonSerializer.Deserialize<ClientMessage>(jsonMessage);

                            await Broadcast(message, roomId, ws);
                        }
                        else if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
                        {
                            value.Remove(ws);
                            if (value.Count == 0)
                            {
                                rooms.Remove(roomId);
                            }

                            await ws.CloseAsync(result.CloseStatus.GetValueOrDefault(), result.CloseStatusDescription,
                                CancellationToken.None);
                        }
                    }

                    await ReceiveMessage(ws,
                        HandleMessage);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                }
            });

            async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
            {
                var buffer = new byte[1024 * 4];
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    handleMessage(result, buffer);
                }
            }

            async Task Broadcast(ClientMessage response, string roomId, WebSocket sender)
            {
                var jsonString = JsonSerializer.Serialize(response);
                var bytes = Encoding.UTF8.GetBytes(jsonString);

                rooms.TryGetValue(roomId, out List<WebSocket> roomsSockets);

                if (roomsSockets == null) return;

                foreach (WebSocket socket in roomsSockets)
                {
                    if (socket.State != WebSocketState.Open || socket == sender) continue;
                    ArraySegment<byte> arraySegment = new(bytes, 0, bytes.Length);
                    await socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }

            app.UseCors("corsExample");

            await app.RunAsync();
        });
    }
}