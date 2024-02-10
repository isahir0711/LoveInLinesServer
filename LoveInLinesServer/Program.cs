using LoveInLinesServer.DTOs;
using Microsoft.AspNetCore.Builder;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

string frontURL = builder.Configuration["frontURL"];


builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "corsExample",
                     policy =>
                     {
                         policy.WithOrigins(frontURL).AllowAnyHeader().AllowAnyMethod();
                     });
});

var app = builder.Build();

app.UseWebSockets();


var rooms = new Dictionary<string, List<WebSocket>>();

app.Map("/ws", async context =>
{

    if (context.WebSockets.IsWebSocketRequest)
    {
        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        string roomId = context.Request.Query["roomId"];


        if (!rooms.ContainsKey(roomId))
        {
            rooms[roomId] = new List<WebSocket>();
        }
        rooms[roomId].Add(ws);

        Console.WriteLine($"{ws.State} Conectado en sala ==> {roomId}");
        Console.WriteLine($"Numero de salas {rooms.Count}");
        foreach (var item in rooms[roomId])
        {
            Console.WriteLine($"socket: {item}--");
        }

        await ReceiveMessage(ws,
            async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    ClientMessage message = JsonSerializer.Deserialize<ClientMessage>(jsonMessage);

                    await Broadcast(message, roomId, ws);
                }
                else if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
                {
                    rooms[roomId].Remove(ws);
                    if (rooms[roomId].Count == 0)
                    {
                        rooms.Remove(roomId);
                    }
                    await ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                }
            });
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
    var payload = response;
    var jsonString = JsonSerializer.Serialize(payload);
    var bytes = Encoding.UTF8.GetBytes(jsonString);
    if (rooms.ContainsKey(roomId))
    {
        foreach (var socket in rooms[roomId])
        {
            if (socket.State == WebSocketState.Open && socket != sender)
            {
                var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
                await socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}

app.UseCors("corsExample");

await app.RunAsync();