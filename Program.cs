using NewsAPI;
using NewsAPI.Constants;
using NewsAPI.Models;
using AIdmin;
using System;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        var settings = Setup.LoadSettings();

        Admin admin = null;
        try
        {
            admin = new(settings);
            await admin.SetupAsync();
            var task = Task.Run(admin.StartAsync);
            Console.WriteLine("Бот запущен, help - справка");
        } catch(Exception ex)
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"Получено исключение {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine();
            Console.ResetColor();
        }

        while (true)
        {
            try
            {
                string cmd = Console.ReadLine();
                if (cmd == "gen")
                {
                    admin.SendMessageToChannel(PostType.Random);
                    continue;
                }
                else if (cmd == "exit")
                {
                    admin.Stop();
                    return;
                }
                else if (cmd == "help")
                {
                    Console.WriteLine($"Справка по AIdmin:");
                    Console.WriteLine($"gen - сгенерировать новый пост в канале");
                    Console.WriteLine($"exit - остановка и выход");
                }
                Console.WriteLine($"Команда {cmd} не найдена");
            } catch ( Exception ex )
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"Получено исключение {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine();
                Console.ResetColor();
            }
        }
    }
}