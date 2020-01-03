﻿using DANotify.Backend;
using DANotify.Backend.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DANotify.Models {
    public class FeedViewModel {
        public DateTimeOffset Latest { get; set; }
        public FeedBatch FeedBatch { get; set; }
        public FeedUserInfo FeedUserInfo { get; set; }

        public static async Task<FeedViewModel> BuildAsync(FeedSource feedSource, string cursor = null, DateTimeOffset? earliest = null, DateTimeOffset? latest = null) {
            return new FeedViewModel {
                Latest = latest ?? DateTimeOffset.UtcNow,
                FeedBatch = await feedSource.GetBatchesAsync(new FeedParameters {
                    Cursor = cursor,
                    StartAt = earliest ?? DateTimeOffset.MinValue
                }),
                FeedUserInfo = new FeedUserInfo {
                    AuthenticatedUser = await feedSource.GetAuthenticatedUserAsync(),
                    NotificationsUrl = feedSource.GetNotificationsUrl()
                }
            };
        }

        public IEnumerable<IGrouping<string, Artwork>> ArtworkByUser =>
            FeedBatch.Artworks
            .GroupBy(d => d.Author.Username)
            .OrderByDescending(g => g.Select(x => x.Timestamp).Max());

        public IEnumerable<IGrouping<string, StatusUpdate>> StatusesByUser =>
            FeedBatch.StatusUpdates
            .GroupBy(s => s.Author.Username)
            .OrderByDescending(g => g.Select(x => x.Timestamp).Max());

        public IEnumerable<FeedItem> Misc =>
            FeedBatch.FeedItems
            .Except(FeedBatch.Artworks)
            .Except(FeedBatch.JournalEntries)
            .Except(FeedBatch.StatusUpdates);
    }
}