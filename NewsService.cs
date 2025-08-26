using NewsAPI;
using NewsAPI.Constants;
using System;
using System.Collections.Generic;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace AIdmin
{

    public class NewsWrapper(string key)
    {
        private NewsApiClient client = new(key);
        public List<NewsAPI.Models.Article> GetLatestNewsAsync(Languages language, string topic)
        {
            var result = client.GetEverything(new NewsAPI.Models.EverythingRequest()
            {
                Q = topic,
                Language = language,
                SortBy = SortBys.Relevancy
            });
            if (result.Status == Statuses.Ok) return result.Articles;
            return new();
        }
    }
}
