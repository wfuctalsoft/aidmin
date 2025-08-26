using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AIdmin
{
    public static class Setup
    {
        public static Settings LoadSettings()
        {
            if (File.Exists("settings.json"))
            {
                Settings settings = new Settings();
                try
                {
                    settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText("settings.json"),new JsonSerializerOptions()
                    {
                        IncludeFields = true,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    });
                    Console.Write("Редактировать настройки? (Space - да):");
                    if (Console.ReadKey(true).Key != ConsoleKey.Spacebar)
                    {
                        Console.WriteLine();
                        return settings;
                    }
                }
                catch
                {
                    Console.WriteLine("Файл настроек некорректен");
                }
                return SetUp(settings);
            }
            return SetUp(new Settings());
        }
        private static string ValueToString(object? value)
        {
            if (value is long lv) return lv.ToString();
            if (value is string sv) return sv;
            if (value is string[] av) return $"{string.Join(", ", av.Take(3).Select(_ => $"\"{_}\""))}{(av.Length>3?$" +{av.Length-3}...":"")}";
            if (value == null) return "неизвестно";
            return value.ToString();
        }
        private static Settings SetUp(Settings settings)
        {
            Console.Clear();
            Console.WriteLine();
            Console.WriteLine("Настройте вашего бота");

            var fields = settings.GetType().GetFields();
            while (true)
            {
                for (int i = 0; i < fields.Length; i++) Console.WriteLine($"   {i+1}. {fields[i].Name} = {ValueToString(fields[i].GetValue(settings))} {(fields[i].IsDefined(typeof(RequiredAttribute),true)?"(обязательное)" : "")}");

                int choice;
                do Console.Write("Выбор (0 - закончить):");
                while (!int.TryParse(Console.ReadLine(), out choice) || choice < 0 || choice > fields.Length);
                Console.WriteLine();

                if (choice == 0)
                {
                    if (!ValidateRequiredFields(settings))
                    {
                        Console.WriteLine("Не все обязательные поля заполнены");
                        continue;
                    }
                    else break;
                }

                Edit(ref fields[choice-1],settings);
            }
            File.WriteAllText("settings.json", JsonSerializer.Serialize(settings, new JsonSerializerOptions()
            {
                IncludeFields = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }));
            return settings;
        }
        private static bool ValidateRequiredFields<T>(T obj) =>
        typeof(T)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.IsDefined(typeof(RequiredAttribute), inherit: true))
            .All(f => !IsDefaultValue(f.GetValue(obj)));

        private static bool IsDefaultValue(object? value) =>
            value switch
            {
                null => true,
                string s => string.IsNullOrEmpty(s),
                ValueType v => v.Equals(Activator.CreateInstance(v.GetType())),
                _ => false
            };
        private static void Edit(ref FieldInfo field, object obj)
        {
            if(field.FieldType == typeof(long) || field.FieldType == typeof(long?))
            {
                Console.Write($"Введите новое численное значение для {field.Name}:");
                var pos = Console.GetCursorPosition();
                while (true)
                {
                    string line = Console.ReadLine();

                    if(long.TryParse(line,out var value))
                    {
                        field.SetValue(obj, value);
                        return;
                    }

                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.SetCursorPosition(pos.Left, pos.Top);
                    Console.Write("Некорректное значение");
                    Thread.Sleep(3000);
                    Console.ResetColor();
                    Console.SetCursorPosition(pos.Left, pos.Top);
                    Console.Write("                     ");
                    Console.SetCursorPosition(pos.Left, pos.Top);
                }
            }
            if (field.FieldType == typeof(string))
            {
                Console.Write($"Введите новое строковое значение для {field.Name}:");
                string line = Console.ReadLine();
                field.SetValue(obj, line);
                Console.WriteLine(field.GetValue(obj));
                return;
            }
            if (field.FieldType == typeof(string[]))
            {
                Console.WriteLine($"Введите новое многострочное значение для {field.Name}");
                List<string> list = new();
                while (true)
                {
                    Console.Write("Добавить строку? (ESC - нет):");
                    if(Console.ReadKey(true).Key == ConsoleKey.Escape)
                    {
                        field.SetValue(obj, list.ToArray());
                        return;
                    }
                    Console.CursorLeft = 0;
                    Console.Write("                             ");
                    Console.CursorLeft = 0;
                    Console.Write($"{list.Count}. ");
                    list.Add(Console.ReadLine());
                }
            }
        }
    }

    public class Settings
    {
        [Required]
        public string OllamaUrl = "http://localhost:11434";

        [Required]
        public string BotToken;

        [Required]
        public long ChannelId;

        [Required]
        public long MinMillisecondsDelay;

        [Required]
        public long MaxMillisecondsDelay;

        public string? NewsApiToken;
        public long? CommentsId;
        public string[]? Events;
        public string? Role;
        public string? FooterText;
        public string? FooterLink;
        public string? NewsLanguageCode;
        public string[]? NewsTopics;

        public Settings()
        {
        }
    }
}
