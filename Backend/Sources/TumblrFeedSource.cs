﻿using ArtworkInbox.Backend.Types;
using DontPanic.TumblrSharp.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ArtworkInbox.Backend.Sources {
    public class TumblrFeedSource : FeedSource {
        private readonly TumblrClient _client;

        public TumblrFeedSource(TumblrClient client) {
            _client = client;
        }

        public class UserInfoResponse {
            public User user;
        }

        public class User {
            public string name;
            public IEnumerable<Blog> blogs;
        }

        public class Blog {
            public IEnumerable<Avatar> avatar;
            public string name;
            public bool primary;
            public string url;
        }

        public class Avatar {
            public int width;
            public int height;
            public string url;
        }

        public override async Task<Author> GetAuthenticatedUserAsync() {
            var response = await _client.CallApiMethodAsync<UserInfoResponse>(
                new DontPanic.TumblrSharp.ApiMethod(
                    $"https://api.tumblr.com/v2/user/info",
                    _client.OAuthToken,
                    System.Net.Http.HttpMethod.Get),
                CancellationToken.None);
            return new Author {
                AvatarUrl = response.user.blogs
                    .Where(x => x.primary)
                    .SelectMany(x => x.avatar)
                    .OrderByDescending(x => x.width * x.height)
                    .Select(x => x.url)
                    .DefaultIfEmpty(null)
                    .First(),
                ProfileUrl = response.user.blogs
                    .Where(x => x.primary)
                    .Select(x => x.url)
                    .DefaultIfEmpty(null)
                    .First(),
                Username = response.user.name
            };
        }

        public class Dashboard {
            public IEnumerable<Post> posts;
        }

        public class Post {
            public string type; // = "blocks"
            public long id;
            public Blog blog;
            public string post_url;
            public long timestamp;
            public string summary;
            public IEnumerable<Content> content;
            public IEnumerable<Trail> trail;
        }

        public class Content {
            public string type;
            public IEnumerable<Media> media; // type = "image"
            public string text; // type = "text"
        }

        public class Trail {
            public Blog blog;
            public IEnumerable<Content> content;
        }

        public class Media {
            public string type;
            public int width;
            public int height;
            public string url;
        }

        private static IEnumerable<FeedItem> Wrangle(IEnumerable<Post> posts) {
            foreach (var p in posts) {
                var author = new Author {
                    Username = p.blog.name,
                    ProfileUrl = $"https://{p.blog.name}.tumblr.com"
                };
                if (p.type == "blocks") {
                    foreach (var c in p.content) {
                        if (c.type == "image") {
                            yield return new Artwork {
                                Author = author,
                                LinkUrl = p.post_url,
                                Thumbnails = c.media.Select(x => new Thumbnail {
                                    Height = x.height,
                                    Width = x.width,
                                    Url = x.url
                                }),
                                Timestamp = DateTimeOffset.FromUnixTimeSeconds(p.timestamp),
                                Title = ""
                            };
                        } else if (c.type == "text") {
                            yield return new StatusUpdate {
                                Author = author,
                                Html = WebUtility.HtmlEncode(c.text),
                                LinkUrl = p.post_url,
                                Timestamp = DateTimeOffset.FromUnixTimeSeconds(p.timestamp)
                            };
                        }
                    }
                    foreach (var t in p.trail)
                    foreach (var c in t.content) {
                        if (c.type == "image") {
                            yield return new Artwork {
                                Author = author,
                                LinkUrl = p.post_url,
                                RepostedFrom = t.blog.name,
                                Thumbnails = c.media.Select(x => new Thumbnail {
                                    Height = x.height,
                                    Width = x.width,
                                    Url = x.url
                                }),
                                Timestamp = DateTimeOffset.FromUnixTimeSeconds(p.timestamp),
                                Title = ""
                            };
                        } else if (c.type == "text") {
                            yield return new StatusUpdate {
                                Author = author,
                                Html = WebUtility.HtmlEncode(c.text),
                                LinkUrl = p.post_url,
                                RepostedFrom = t.blog.name,
                                Timestamp = DateTimeOffset.FromUnixTimeSeconds(p.timestamp)
                            };
                        }
                    }
                } else {
                    throw new NotImplementedException();
                }
            }
        }

        public override async Task<FeedBatch> GetBatchAsync(string cursor) {
            long offset = 0;
            if (cursor != null && long.TryParse(cursor, out long l))
                offset = l;

            var response = await _client.CallApiMethodAsync<Dashboard>(
                new DontPanic.TumblrSharp.ApiMethod(
                    $"https://api.tumblr.com/v2/user/dashboard",
                    _client.OAuthToken,
                    System.Net.Http.HttpMethod.Get,
                    new DontPanic.TumblrSharp.MethodParameterSet {
                        { "npf", "true" },
                        { "offset", offset }
                    }),
                CancellationToken.None);

            return new FeedBatch {
                Cursor = $"{offset + response.posts.Count()}",
                HasMore = response.posts.Any(),
                FeedItems = Wrangle(response.posts)
            };
        }

        public override string GetNotificationsUrl() => "https://www.tumblr.com/inbox";
        public override string GetSubmitUrl() => "https://www.tumblr.com/dashboard";
    }
}