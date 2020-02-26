﻿using System;
using System.Threading.Tasks;

namespace ArtworkInbox.Backend.Sources {
    public class TumblrPostDestination : IPostDestination {
        private readonly TumblrFeedSource _feedSource;
        private readonly TumblrFeedSource.Blog _blog;

        public TumblrPostDestination(TumblrFeedSource feedSource, TumblrFeedSource.Blog blog) {
            _feedSource = feedSource;
            _blog = blog;
        }

        public Task<string> GetProfileUrlAsync() {
            return Task.FromResult(_blog.url);
        }

        public async Task<Uri> PostStatusAsync(string text) {
            var result = await _feedSource.PostStatusAsync(text, _blog.name);
            var baseUri = new Uri(await GetProfileUrlAsync());
            return new Uri(baseUri, $"/post/{result.PostId}");
        }
    }
}