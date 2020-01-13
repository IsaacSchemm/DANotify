﻿using System;
using System.Linq;
using System.Threading.Tasks;
using ArtworkInbox.Backend;
using ArtworkInbox.Backend.Sources;
using ArtworkInbox.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tweetinvi.Models;

namespace ArtworkInbox.Controllers {
    public class TumblrController : FeedController {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ArtworkInboxTumblrClientFactory _factory;

        public TumblrController(UserManager<ApplicationUser> userManager, ApplicationDbContext context, ArtworkInboxTumblrClientFactory factory) {
            _userManager = userManager;
            _context = context;
            _factory = factory;
        }

        public IActionResult Index() {
            return RedirectToAction(nameof(Feed));
        }

        protected override Task<ApplicationUser> GetUserAsync() =>
            _userManager.GetUserAsync(User);

        protected override string GetSiteName() => "Tumblr";

        protected override async Task<FeedSource> GetFeedSourceAsync() {
            var userId = _userManager.GetUserId(User);
            var dbToken = await _context.UserTumblrTokens
                .Where(t => t.UserId == userId)
                .SingleOrDefaultAsync();
            if (dbToken == null)
                throw new NoTokenException();

            return new TumblrFeedSource(_factory.Create(dbToken));
        }

        protected override async Task<DateTimeOffset> GetLastRead() {
            var userId = _userManager.GetUserId(User);
            var dt = await _context.UserReadMarkers
                .Where(t => t.UserId == userId)
                .Select(t => t.TumblrLastRead)
                .SingleOrDefaultAsync();
            return dt ?? DateTimeOffset.MinValue;
        }

        protected override async Task SetLastRead(DateTimeOffset lastRead) {
            var userId = _userManager.GetUserId(User);
            var o = await _context.UserReadMarkers
                .Where(t => t.UserId == userId)
                .SingleOrDefaultAsync();

            if (o == null) {
                o = new UserReadMarker {
                    UserId = userId
                };
                _context.UserReadMarkers.Add(o);
            }

            o.TumblrLastRead = lastRead;
            await _context.SaveChangesAsync();
        }
    }
}