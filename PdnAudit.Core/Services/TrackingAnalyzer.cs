using Microsoft.Playwright;
using PdnAudit.Core.Models.Tracking;
using PdnAudit.Core.Services.Dictionaries;
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
                        await page.Locator("[data-pdn-accept]").First.ClickAsync(new() { Timeout = 5000, Force = true });
                        await page.WaitForTimeoutAsync(3000);
                    }
                    catch (Exception ex) { phase.Errors.Add("Клик по «Принять»: " + ex.Message); }
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

        // 1. Ищем все видимые кнопки на странице
        const allButtons = [...document.querySelectorAll('button, a, [role="button"], input[type="button"]')].filter(visible);

        // 2. Ищем кнопку "Принять" по тексту
        let acceptBtn = null;
        for (const b of allButtons) {
            const t = norm(b.innerText || b.value || b.getAttribute('aria-label'));
            if (/принять|принимаю|согласен|соглашаюсь|разрешить|понятно|хорошо|^ок$|^ok$|accept|agree|allow|got it/.test(t)) {
                acceptBtn = b;
                res.AcceptFound = true; 
                res.AcceptText = t;
                break;
            }
        }

        // 3. Если нашли кнопку, проверяем, есть ли рядом текст про cookie
        if (acceptBtn) {
            // Поднимаемся вверх по дереву DOM, чтобы найти контейнер баннера
            let parent = acceptBtn.parentElement;
            for (let i = 0; i < 4 && parent; i++) {
                const text = norm(parent.innerText);
                if (/cookie|куки|файлы cookie/.test(text) && text.length < 1000) {
                    res.Found = true;
                    res.Text = text.slice(0, 500);
                    res.HasPolicyLink = [...parent.querySelectorAll('a[href]')].some(a =>
                        /cookie|куки|политик|privacy|персональн|policy/.test(norm(a.innerText) + ' ' + (a.getAttribute('href') || '').toLowerCase()));

                    // Проверяем, есть ли кнопки отклонения
                    res.HasReject = allButtons.some(b => /отклон|отказ|не соглас|только необходим|reject|decline/.test(norm(b.innerText)));
                    res.HasSettings = allButtons.some(b => /настро|управл|settings|manage/.test(norm(b.innerText)));
                    break;
                }
                parent = parent.parentElement;
            }
        }

        // Если не нашли по кнопке, ищем по тексту "cookie" в любых видимых блоках
        if (!res.Found) {
            const elements = [...document.querySelectorAll('div, section, aside, footer')].filter(visible);
            for (const el of elements) {
                const text = norm(el.innerText);
                if (/cookie|куки/.test(text) && text.length < 1500 && text.length > 20) {
                    res.Found = true;
                    res.Text = text.slice(0, 500);
                    break;
                }
            }
        }

        return JSON.stringify(res);
        }
        """);

            return JsonSerializer.Deserialize<CookieBannerInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new CookieBannerInfo();
        }
    }
}
