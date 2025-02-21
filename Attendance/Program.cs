using System.Device.Location;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    private static readonly string BotToken = "YOUR_TELEGRAM_BOT_TOKEN";
    private static readonly double AllowedLatitude = 40.7128;  // Координаты аудитории
    private static readonly double AllowedLongitude = -74.0060;
    private static readonly double AllowedRadius = 50.0; // Радиус в метрах
    private static readonly string AttendanceFile = "attendance.txt";
    private static readonly string ViolationsFile = "violations.txt";

    private static Dictionary<long, string> StudentNames = [];
    private static HashSet<long> MarkedStudents = [];

    static async Task Main()
    {
        LoadMarkedStudents(); // Загружаем уже отметившихся студентов
        var botClient = new TelegramBotClient(BotToken);
        botClient.StartReceiving(UpdateHandler, ErrorHandler);
        Console.WriteLine("Бот запущен...");
        Console.ReadLine();
    }

    private static async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message || update.Message == null) return;

        var message = update.Message;
        var chatId = message.Chat.Id;

        if (message.Text == "/start")
        {
            if (!StudentNames.ContainsKey(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "Привет! Введи своё (Фамилия Имя):");
            }
            else
            {
                await AskForLocation(botClient, chatId);
            }
        }
        else if (!StudentNames.ContainsKey(chatId) && message.Text != null)
        {
            StudentNames[chatId] = message.Text;
            await AskForLocation(botClient, chatId);
        }
        else if (message.Location != null)
        {
            if (MarkedStudents.Contains(chatId))
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Вы уже отметились! Повторная попытка запрещена.");
                return;
            }

            var userLat = message.Location.Latitude;
            var userLon = message.Location.Longitude;

            if (IsInAllowedArea(userLat, userLon))
            {
                MarkAttendance(chatId);
                var studentName = StudentNames[chatId];
                await botClient.SendTextMessageAsync(chatId, $"✅ {studentName}, посещаемость успешно зафиксирована!");
            }
            else
            {
                LogViolation(chatId);
                await botClient.SendTextMessageAsync(chatId, "❌ Вы не в аудитории! Данные о нарушении сохранены.");
            }
        }
    }

    private static async Task AskForLocation(ITelegramBotClient botClient, long chatId)
    {
        await botClient.SendTextMessageAsync(chatId, "Отправь свою геолокацию, чтобы отметить посещаемость.",
            replyMarkup: new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton("📍 Отправить геолокацию") { RequestLocation = true }
            })
            { ResizeKeyboard = true });
    }

    private static bool IsInAllowedArea(double userLat, double userLon)
    {
        var sCoord = new GeoCoordinate(AllowedLatitude, AllowedLongitude);
        var eCoord = new GeoCoordinate(userLat, userLon);
        return sCoord.GetDistanceTo(eCoord) <= AllowedRadius;
    }

    private static void MarkAttendance(long chatId)
    {
        var studentName = StudentNames[chatId];
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string record = $"{timestamp} | {chatId} | {studentName}";
        File.AppendAllText(AttendanceFile, record + Environment.NewLine);

        MarkedStudents.Add(chatId);
    }

    private static void LogViolation(long chatId)
    {
        var studentName = StudentNames.ContainsKey(chatId) ? StudentNames[chatId] : "Неизвестный";
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string record = $"{timestamp} | {chatId} | {studentName}";
        File.AppendAllText(ViolationsFile, record + Environment.NewLine);

        MarkedStudents.Add(chatId);
    }

    private static void LoadMarkedStudents()
    {
        if (!File.Exists(AttendanceFile)) return;

        var lines = File.ReadAllLines(AttendanceFile);
        foreach (var line in lines)
        {
            var parts = line.Split(" | ");
            if (parts.Length > 1 && long.TryParse(parts[1], out long chatId))
            {
                MarkedStudents.Add(chatId);
            }
        }
    }

    private static Task ErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}