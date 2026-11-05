using Microsoft.Playwright;
using PdnAudit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services {
    public class TrackingAnalyzer {
        private readonly IBrowser _browser;
        public TrackingAnalyzer(IBrowser browser) => _browser = browser;

        public async Task<TrackingResult> AnalyzeAsync(string url) => new()
        {
            Before = await CollectAsync(url, acceptConsent: false),
            After = await CollectAsync(url, acceptConsent: true)
        };

        public async Task<TrackingPhase> CollectAsync(string url, bool acceptConsent) {
            var phase = new TrackingPhase { AcceptedConsent = acceptConsent };
            string siteHost = new Uri(url).Host;
            var byHost = new Dictionary<string, RequestInfo>();

            // Новый контекст = чистые cookies и storage
            var context = await _browser.NewContextAsync();
            var page = await context.NewPageAsync();
            try
            {
                // Подписка до загрузки, чтобы не пропустить ранние запросы
                page.Request += (_, req) =>
                {
                    if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var u)) return;
                    if (u.Scheme != "http" && u.Scheme != "https") return;
                    if (TrackerDictionary.IsSameSite(u.Host, siteHost)) return;

                    lock (byHost)
                    {
                        if (byHost.TryGetValue(u.Host, out var existing)) { existing.Count++; return; }

                        var rule = TrackerDictionary.MatchHost(u.Host);
                        byHost[u.Host] = new RequestInfo
                        {
                            Host = u.Host,
                            ExampleUrl = req.Url,
                            ResourceType = req.ResourceType,
                            ServiceName = rule?.Name ?? u.Host,
                            Category = rule?.Category ?? TrackerCategory.Other,
                            IsForeign = rule?.IsForeign ?? TrackerDictionary.LooksForeign(u.Host),
                            Count = 1
                        };
                    }
                };

                try
                {
                    await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
                }
                catch (PlaywrightException ex) { phase.Errors.Add("Загрузка: " + ex.Message); }

                await page.WaitForTimeoutAsync(3000);
                phase.Banner = await DetectBannerAsync(page);

                if (acceptConsent && phase.Banner.AcceptFound)
                {
                    try
                    {
                        await page.Locator("[data-pdn-accept]").First.ClickAsync(new() { Timeout = 3000 });
                        await page.WaitForTimeoutAsync(3000);
                    }
                    catch (PlaywrightException ex) { phase.Errors.Add("Клик по «Принять»: " + ex.Message); }
                }

                try { await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)"); } catch { }
                await page.WaitForTimeoutAsync(2000);

                lock (byHost) phase.Requests = byHost.Values.ToList();

                foreach (var c in await context.CookiesAsync())
                {
                    var byName = TrackerDictionary.MatchCookie(c.Name);
                    var rule = TrackerDictionary.MatchHost(c.Domain.TrimStart('.'));
                    phase.Cookies.Add(new CookieInfo
                    {
                        Name = c.Name,
                        Domain = c.Domain,
                        Expires = c.Expires > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)c.Expires) : null,
                        IsThirdParty = !TrackerDictionary.IsSameSite(c.Domain, siteHost),
                        ServiceName = byName?.Name ?? rule?.Name ?? "",
                        Category = byName?.Category ?? rule?.Category ?? TrackerCategory.Technical
                    });
                }
                return phase;
            }
            finally
            {
                await context.CloseAsync();
            }
        }

        private static async Task<CookieBannerInfo> DetectBannerAsync(IPage page) {
            string json = await page.EvaluateAsync<string>("""
        () => {
          const norm = s => (s || '').replace(/[\u00AD\u200B-\u200D\uFEFF]/g, '').replace(/\s+/g, ' ').trim().toLowerCase();
          const visible = el => el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden';
          const res = { Found: false, Text: '', AcceptFound: false, AcceptText: '',
                        HasReject: false, HasSettings: false, HasPolicyLink: false };

          const cands = [...document.querySelectorAll(
              '[id*="cookie" i], [class*="cookie" i], [id*="consent" i], [class*="consent" i], ' +
              '[id*="gdpr" i], [class*="gdpr" i], [role="dialog"], [aria-label*="cookie" i]')]
            .filter(el => visible(el) && /cookie|куки/.test(norm(el.innerText)));
          if (!cands.length) return JSON.stringify(res);

          cands.sort((a, b) => a.innerText.length - b.innerText.length);
          const box = cands.find(el => el.querySelector('button, a, [role="button"], input[type="button"]')) || cands[0];

          res.Found = true;
          res.Text = norm(box.innerText).slice(0, 500);

          const btns = [...box.querySelectorAll('button, a, [role="button"], input[type="button"], input[type="submit"]')].filter(visible);
          for (const b of btns) {
            const t = norm(b.innerText || b.value || b.getAttribute('aria-label'));
            if (!t) continue;
            if (/отклон|отказ|не соглас|только необходим|только обязательн|reject|decline|deny|necessary only|refuse/.test(t)) res.HasReject = true;
            else if (/настро|управл|settings|preferences|customi[sz]e|manage/.test(t)) res.HasSettings = true;
            else if (!res.AcceptFound && /принять|принимаю|согласен|соглашаюсь|разрешить|понятно|хорошо|^ок$|^ok$|accept|agree|allow|got it/.test(t)) {
              res.AcceptFound = true; res.AcceptText = t;
              b.setAttribute('data-pdn-accept', '1');
            }
          }
          res.HasPolicyLink = [...box.querySelectorAll('a[href]')].some(a =>
              /cookie|куки|политик|privacy|персональн|policy/.test(norm(a.innerText) + ' ' + (a.getAttribute('href') || '').toLowerCase()));
          return JSON.stringify(res);
        }
        """);

            return JsonSerializer.Deserialize<CookieBannerInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new CookieBannerInfo();
        }
    }
}
