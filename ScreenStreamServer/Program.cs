using System.Net.WebSockets;
using System.Text;
using ScreenStreamServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ScreenCaptureService>();

var app = builder.Build();

app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await HandleWebSocket(webSocket,
            context.RequestServices.GetRequiredService<ScreenCaptureService>());
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Run();

// ------- Обработчик WebSocket-соединения -------
async Task HandleWebSocket(WebSocket webSocket, ScreenCaptureService captureService)
{
    // Флаг: транслируем ли сейчас кадры
    bool isStreaming = false;
    // Блокировка для безопасного доступа к флагу из разных задач
    var streamLock = new object();

    // ---------- Задача 1: чтение команд от клиента ----------
    var receiveTask = Task.Run(async () =>
    {
        var buffer = new byte[1024];
        while (webSocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            catch
            {
                break; // соединение закрыто
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Закрыто клиентом",
                    CancellationToken.None);
                break;
            }

            // Получаем текст команды
            var text = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim().ToLower();

            // Меняем флаг в зависимости от команды
            string reply;
            lock (streamLock)
            {
                if (text == "start")
                {
                    isStreaming = true;
                    reply = "Трансляция запущена";
                }
                else if (text == "stop")
                {
                    isStreaming = false;
                    reply = "Трансляция остановлена";
                }
                else
                {
                    reply = $"Неизвестная команда: {text}";
                }
            }

            // Отправляем ответ клиенту (текстовое сообщение)
            var replyBytes = Encoding.UTF8.GetBytes(reply);
            await webSocket.SendAsync(
                new ArraySegment<byte>(replyBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
    });

    // ---------- Задача 2: отправка кадров, если идёт трансляция ----------
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            bool shouldSend;
            lock (streamLock) { shouldSend = isStreaming; }

            if (shouldSend)
            {
                try
                {
                    var frameBytes = captureService.CaptureFrame(quality: 50);

                    await webSocket.SendAsync(
                        new ArraySegment<byte>(frameBytes),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при отправке кадра: {ex.Message}");
                    break;
                }

                // ~15 FPS
                await Task.Delay(1000 / 15);
            }
            else
            {
                // Если трансляция остановлена — просто ждём и не грузим CPU
                await Task.Delay(100);
            }
        }
    }
    finally
    {
        // Дожидаемся завершения задачи чтения
        try { await receiveTask; } catch { /* игнорируем */ }
    }
}