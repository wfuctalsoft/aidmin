using NewsAPI.Constants;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AIdmin
{
    public class Admin
    {
        private TelegramBotClient botClient;
        private string token;

        private ChatId channel;
        private ChatId? comments = null;

        private OllamaClient ollamaPosts;
        private OllamaClient ollamaComments;

        private string model = string.Empty;
        private Dictionary<string, long> reactions = new();

        private string[]? events;

        private string? footer;

        private Languages newsLanguage;
        private string[] newsTopics;

        private bool running = false;

        private int minDelay;
        private int maxDelay;

        private NewsWrapper? news = null;

        private PostType[] availablePosts;

        public Admin(Settings settings)
        {
            botClient = new TelegramBotClient(settings.BotToken);
            token = settings.BotToken;

            channel = settings.ChannelId;
            comments = settings.CommentsId;

            string role = settings.Role == null ? "" : $"Стиль постов: {settings.Role}";
            ollamaPosts = new OllamaClient(role: Info.PostPrompt + role);
            ollamaComments = new OllamaClient(role: Info.CommentPrompt+role, shared: ollamaPosts);

            events = settings.Events;

            if(settings.FooterText != null)
                footer = "\n\n" + (settings.FooterLink == null ? settings.FooterText : $"[{settings.FooterText}]({settings.FooterLink})");
            else if (settings.FooterLink != null) footer = "\n\n" + settings.FooterLink;

            if(settings.NewsApiToken != null) news = new NewsWrapper(settings.NewsApiToken);
            newsLanguage = Enum.TryParse(typeof(Languages), settings.NewsLanguageCode ?? "en",true,out var lang)? (Languages)lang : Languages.EN;
            newsTopics = settings.NewsTopics ?? ["world"];

            minDelay = (int)settings.MinMillisecondsDelay;
            maxDelay = (int)settings.MaxMillisecondsDelay;
            if(maxDelay <  minDelay) maxDelay = minDelay;

            List<PostType> posts = [PostType.Creative];
            if (news != null) posts.Add(PostType.News);
            if (events != null || events?.Length == 0) posts.Add(PostType.Event);
            availablePosts = posts.ToArray();
        }

        public async Task SetupAsync()
        {
            var avail = await ollamaPosts.GetAvailableModelsAsync();
            int choice;

            Console.WriteLine($"Выберите одну модель из установленных (по порядку): \r\n{string.Join(Environment.NewLine, avail.Select(_ => _.Name))}");
            do Console.Write($"Выбор (1-{avail.Count}):");
            while (!int.TryParse(Console.ReadLine(), out choice) || choice < 1 || choice > avail.Count);

            model = avail[choice-1].Name;

            if (File.Exists("history.json")) ollamaPosts.LoadHistory("history");
        }
        public async Task StartAsync()
        {
            botClient.StartReceiving(UpdateHandler, ErrorHandler, new Telegram.Bot.Polling.ReceiverOptions()
            {
                DropPendingUpdates = true,
                AllowedUpdates = [UpdateType.Message, UpdateType.MessageReactionCount, UpdateType.ChatBoost]
            });

            await Task.Run(async () =>
            {
                running = true;
                while (running)
                {
                    Console.WriteLine("Генерация поста...");
                    await SendMessageToChannel();
                    int delay = Random.Shared.Next(minDelay, maxDelay);
                    Console.WriteLine($"Следующий пост запланирован в {DateTime.Now.AddMilliseconds(delay)}");
                    Thread.Sleep(delay);
                }
            });
        }
        public void Stop()
        {
            botClient.Close();
            botClient = new TelegramBotClient(token);
            running = false;
            ollamaPosts.SaveHistory("history");
        }
        private async Task UpdateHandler(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            if (update.Type == UpdateType.Message && !(comments == null))
            {
                var message = update.Message;
                if (message == null) return;
                if (message.Chat.Id != comments ||
                    message.ReplyToMessage == null ||
                    message.ReplyToMessage.ForwardFromChat == null ||
                    message.ReplyToMessage.ForwardFromChat.Id != channel) return;

                string answer = await ollamaComments.ChatAsync($"Комментарий от {message.From?.FirstName}: {message.Text}", model);
                if (answer.Trim() == "<nothing>") return;

                await bot.SendMessage(comments, answer,
                    replyParameters: new ReplyParameters() { ChatId = comments, MessageId = message.Id });
            }
            else if (update.Type == UpdateType.ChatBoost && update.ChatBoost?.Chat.Id == channel)
            {
                SendMessageToChannel(0);
            }
            else if (update.Type == UpdateType.MessageReactionCount)
            {
                var reactionsCount = update.MessageReactionCount ?? new();
                if (reactionsCount.Chat.Id != channel) return;

                foreach (var reaction in reactionsCount.Reactions)
                {
                    if (reaction.Type is ReactionTypeEmoji emoji)
                    {
                        if (reactions.ContainsKey(emoji.Emoji))
                            reactions[emoji.Emoji] = reaction.TotalCount;
                        else reactions.Add(emoji.Emoji, reaction.TotalCount);
                    }

                    else if (reaction.Type is ReactionTypeCustomEmoji customEmoji)
                    {
                        if (reactions.ContainsKey(customEmoji.CustomEmojiId))
                            reactions[customEmoji.CustomEmojiId] = reaction.TotalCount;
                        else reactions.Add(customEmoji.CustomEmojiId, reaction.TotalCount);
                    }

                    else if (reaction.Type is ReactionTypePaid paid)
                    {
                        if (reactions.ContainsKey("Платные"))
                            reactions["Платные"] = reaction.TotalCount;
                        else reactions.Add("Платные", reaction.TotalCount);
                    }
                }
            }
        }
        private static Task ErrorHandler(ITelegramBotClient bot, Exception ex, CancellationToken ct)
        {
            throw new HttpProtocolException(0, ex.Message, ex);
        }

        public async Task SendMessageToChannel(PostType postType = PostType.Random)
        {
            if (postType > 0)
            {
                if (postType == PostType.OnBoost)
                {
                    string post = await ollamaPosts.ChatAsync($"Придумай пост (реакции: {GetLastReactions()}): благодарность за буст канала", model);
                    post = FormatPost(post);

                    await botClient.SendMessage(channel, post, ParseMode.Markdown);
                }
            }

            var choice = GetPost(postType);
            if (choice == PostType.News) // News post
            {
                var latestNews = news?.GetLatestNewsAsync(newsLanguage, newsTopics[Random.Shared.Next(newsTopics.Length)]) ?? new();
                var article = latestNews[Random.Shared.Next(latestNews.Count)];

                string post = await ollamaPosts.ChatAsync($"Адаптируй статью от автора {article.Author} (URL {article.Url}) (реакции: {GetLastReactions()}): \"{article.Title}\"\n\n{article.Content}", model);
                post = FormatPost(post);

                if (!string.IsNullOrEmpty(article.UrlToImage) && Uri.IsWellFormedUriString(article.UrlToImage, UriKind.Absolute))
                    await botClient.SendPhoto(channel, InputFile.FromUri(article.UrlToImage), post, ParseMode.Markdown);
                else
                    await botClient.SendMessage(channel, post, ParseMode.Markdown);
            }

            else if (choice == PostType.Creative) // Creative post
            {
                string post = await ollamaPosts.ChatAsync($"Придумай пост сам (реакции: {GetLastReactions()})", model);
                post = FormatPost(post);

                await botClient.SendMessage(channel, post, ParseMode.Markdown);
            }

            else if(events != null)// Event post
            {
                string post = await ollamaPosts.ChatAsync($"Придумай пост (реакции: {GetLastReactions()}): {events[Random.Shared.Next(events.Length)]}", model);
                post = FormatPost(post);

                await botClient.SendMessage(channel, post, ParseMode.Markdown);
            }
        }
        private PostType GetPost(PostType postType)
        {
            if(postType != PostType.Random)
            {
                if (postType == PostType.News && !availablePosts.Contains(PostType.News) ||
                    postType == PostType.Event && !availablePosts.Contains(PostType.Event))
                    return GetPost(PostType.Random);
                return postType;
            }
            return availablePosts[Random.Shared.Next(availablePosts.Length)];
        }
        private string FormatPost(string post)
        {
            if (footer != null) post += footer;
            return post.Length > 996 ? post.Substring(0, 996) + "..." : post;
        }
        private string GetLastReactions()
        {
            string reactionsString = string.Join(", ", reactions.Select(_ => $"{_.Key}: {_.Value}"));
            if (string.IsNullOrWhiteSpace(reactionsString)) reactionsString = "Нет реакций";

            reactions.Clear();
            return reactionsString;
        }
    }
    public enum PostType
    {
        Random = 0,
        News = -1,
        Creative = -2,
        Event = -3,
        OnBoost = 1
    }
}
