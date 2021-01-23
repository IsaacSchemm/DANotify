﻿using ArtworkInbox.Backend.Types;
using ArtworkInbox.Data;
using Microsoft.SyndicationFeed;
using Microsoft.SyndicationFeed.Atom;
using Microsoft.SyndicationFeed.Rss;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace ArtworkInbox.Backend.Sources {
    public class ExternalFeedSource : ISource {
        private readonly UserExternalFeed _externalFeed;

        public ExternalFeedSource(UserExternalFeed externalFeed) {
            _externalFeed = externalFeed;
        }

        public Task<Author> GetAuthenticatedUserAsync() => Task.FromResult<Author>(null);

        public string GetNotificationsUrl() => null;
        public string GetSubmitUrl() => null;

        public async IAsyncEnumerable<FeedItem> GetFeedItemsAsync() {
            string title = null;

            var req = WebRequest.CreateHttp(_externalFeed.Url);
            using var resp = await req.GetResponseAsync();
            System.Diagnostics.Debug.WriteLine(resp.ContentType);
            using var s = resp.GetResponseStream();
            using var sr = new StreamReader(s);
            string xml = await sr.ReadToEndAsync();
            using var tr = new StringReader(xml);
            using var xmlReader = XmlReader.Create(tr, new XmlReaderSettings() { Async = true });
            var feedReader = _externalFeed.Type == UserExternalFeed.FeedType.Atom
                ? new AtomFeedReader(xmlReader)
                : new RssFeedReader(xmlReader) as XmlFeedReader;
            while (await feedReader.Read()) {
                switch (feedReader.ElementType) {
                    // Read category
                    case SyndicationElementType.Category:
                        ISyndicationCategory category = await feedReader.ReadCategory();
                        break;

                    // Read Image
                    case SyndicationElementType.Image:
                        ISyndicationImage image = await feedReader.ReadImage();
                        break;

                    // Read Item
                    case SyndicationElementType.Item:
                        ISyndicationItem item = await feedReader.ReadItem();

                        DateTimeOffset ts = item.Published > item.LastUpdated ? item.Published : item.LastUpdated;
                        if (ts <= _externalFeed.LastRead)
                            yield break;

                        var image_match = Regex.Match(item is IAtomEntry a ? a.Summary : item.Description, "img[^>]+src=['\"]([^'\"]+)['\"]");
                        if (image_match.Success)
                            yield return new Artwork {
                                Author = new Author {
                                    Username = title ?? string.Join(" / ", item.Contributors.Select(x => x.Name ?? x.Email))
                                },
                                LinkUrl = item.Links.Select(x => x.Uri).Where(x => x != null).Select(x => x.AbsoluteUri).FirstOrDefault(),
                                Thumbnails = new Thumbnail[] {
                                    new Thumbnail {
                                        Url = image_match.Groups[1].Value
                                    }
                                },
                                Timestamp = ts,
                                Title = item.Title
                            };
                        else
                            yield return new BlogPost {
                                Author = new Author {
                                    Username = string.Join(" / ", item.Contributors.Select(x => x.Name ?? x.Email))
                                },
                                LinkUrl = item.Links.Select(x => x.Uri).Where(x => x != null).Select(x => x.AbsoluteUri).FirstOrDefault(),
                                Timestamp = ts,
                                Html = item.Description
                            };
                        break;

                    // Read link
                    case SyndicationElementType.Link:
                        ISyndicationLink link = await feedReader.ReadLink();
                        break;

                    // Read Person
                    case SyndicationElementType.Person:
                        ISyndicationPerson person = await feedReader.ReadPerson();
                        break;

                    // Read content
                    default:
                        ISyndicationContent content = await feedReader.ReadContent();
                        if (content.Name == "title")
                            title = content.Value;
                        break;
                }
            }

            yield break;
        }

        public IAsyncEnumerable<string> GetNotificationsAsync() => AsyncEnumerable.Empty<string>();
    }
}