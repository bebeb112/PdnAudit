using Microsoft.Playwright;
using PdnAudit.Core.Models.Tracking;
using PdnAudit.Core.Services.Dictionaries;
using System.Text.Json;

namespace PdnAudit.Core.Services.Analyzers {
    /// <summary>
    /// Анализатор трекеров и файлов cookie.
    /// Выполняет два прохода по странице (до и после нажатия «Принять cookies»), чтобы выявить скрытые сторонние сервисы.
    /// </summary>
    public class TrackingAnalyzer {
        private readonly IBrowser _browser;
        public TrackingAnalyzer(IBrowser browser) => _browser = browser;

        /// <summary>
        /// Запускает два прохода сканирования: без согласия и с согласием.
        /// </summary>
        public async Task<TrackingResult> AnalyzeAsync(string url) => new()
        {
            Before = await CollectAsync(url, acceptConsent: false),
            After = await CollectAsync(url, acceptConsent: true)
        };

        /// <summary>
        /// Собирает данные о сетевых запросах и cookies на одном из этапов (до или после согласия).
        /// </summary>
        public async Task<TrackingPhase> CollectAsync(string url, bool acceptConsent) {
            var phase = new TrackingPhase { AcceptedConsent = acceptConsent };
            string siteHost = new Uri(url).Host;
            var byHost = new Dictionary<string, RequestInfo>();

            // Новый контекст = чистые cookies и storage
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            });
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

                //Т.к. баннер может появиться через несколько секунд, пробуем до 5 раз с паузой в секунду
                for (int i = 0; i < 5; i++)
                {
                    await page.WaitForTimeoutAsync(1000);
                    try { phase.Banner = await DetectBannerAsync(page); }
                    catch (PlaywrightException ex) { phase.Errors.Add("Поиск баннера: " + ex.Message); }
                    if (phase.Banner.Found) break;
                }

                if (acceptConsent && phase.Banner.AcceptFound)
                {
                    var accept = page.Locator("[data-pdn-accept]").First;
                    try
                    {
                        await accept.ClickAsync(new() { Timeout = 5000 });          
                    }
                    catch (PlaywrightException)
                    {
                        try { await accept.DispatchEventAsync("click"); }            
                        catch (Exception ex) { phase.Errors.Add("Клик по «Принять»: " + ex.Message); }
                    }
                    await page.WaitForTimeoutAsync(3000);
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

        /// <summary>
        /// Анализирует DOM-дерево страницы для поиска баннера cookies.
        /// Находит кнопки «Принять», «Отклонить», «Настроить» и помечает кнопку «Принять» атрибутом для C#.
        /// </summary>
        private static async Task<CookieBannerInfo> DetectBannerAsync(IPage page) {
            string json = await page.EvaluateAsync<string>("""
        () => {
            const norm = s => (s || '')
                .replace(/[\u00AD\u200B-\u200D\uFEFF]/g, '')
                .replace(/\s+/g, ' ').trim().toLowerCase();
            const visible = el => el.getClientRects().length > 0
                && getComputedStyle(el).visibility !== 'hidden';
            const cookieRe = /cookie|куки/;
            const clickSel = 'button, a, [role="button"], input[type="button"], input[type="submit"]';

            const res = { Found: false, Text: '', AcceptFound: false, AcceptText: '',
                          HasReject: false, HasSettings: false, HasPolicyLink: false };

            // ---------- 1. Ищем контейнер баннера ----------
            // Сначала по id/class/role, внутри должен быть текст про cookie и хотя бы одна кнопка
            const bySelector = [...document.querySelectorAll(
                '[id*="cookie" i], [class*="cookie" i], [id*="consent" i], [class*="consent" i], ' +
                '[id*="gdpr" i], [class*="gdpr" i], [role="dialog"], [role="alertdialog"], [aria-label*="cookie" i]')]
                .filter(el => el !== document.body && el !== document.documentElement && visible(el))
                .filter(el => {
                    const t = norm(el.innerText);
                    return cookieRe.test(t) && t.length < 1500 && el.querySelector(clickSel);
                })
                .sort((a, b) => a.innerText.length - b.innerText.length);   // самый маленький = самый точный

            let box = bySelector[0] || null;

            // Запасной вариант: закреплённый на экране блок с текстом про cookie (классы могут быть любыми)
            if (!box) {
                box = [...document.querySelectorAll('div, section, aside, dialog')]
                    .filter(el => {
                        if (!visible(el)) return false;
                        const pos = getComputedStyle(el).position;
                        if (pos !== 'fixed' && pos !== 'sticky') return false;
                        const t = norm(el.innerText);
                        return cookieRe.test(t) && t.length > 20 && t.length < 1500;
                    })
                    .sort((a, b) => a.innerText.length - b.innerText.length)[0] || null;
            }

            if (!box) return JSON.stringify(res);

            res.Found = true;
            res.Text = norm(box.innerText).slice(0, 500);

            // ---------- 2. Кнопки ТОЛЬКО внутри баннера ----------
            const rejectRe   = /отклон|отказ|не соглас|необходим|обязательн|без аналитик|reject|decline|deny|necessary|essential|refuse/;
            const settingsRe = /настро|управл|settings|preferences|customi[sz]e|manage/;
            const acceptRe   = /^(принять|принимаю|согласен|согласна|соглашаюсь|разрешить|понятно|хорошо|ок|ok|accept|agree|allow|got it|i agree)(?=$|[\s.,!])/;

            for (const b of [...box.querySelectorAll(clickSel)].filter(visible)) {
                const t = norm(b.innerText || b.value || b.getAttribute('aria-label'));
                if (!t) continue;

                if (rejectRe.test(t)) { res.HasReject = true; continue; }
                if (settingsRe.test(t)) { res.HasSettings = true; continue; }

                // Ссылки с переходом на другую страницу не считаем кнопкой «Принять»
                const href = b.tagName === 'A' ? (b.getAttribute('href') || '') : '';
                const navigates = href && !/^(#|javascript:)/i.test(href);

                if (!res.AcceptFound && !navigates && acceptRe.test(t)) {
                    res.AcceptFound = true;
                    res.AcceptText = t;
                    b.setAttribute('data-pdn-accept', '1');     // метка, по которой кликает C#
                }
            }

            res.HasPolicyLink = [...box.querySelectorAll('a[href]')].some(a =>
                /cookie|куки|политик|privacy|персональн|policy/.test(
                    norm(a.innerText) + ' ' + (a.getAttribute('href') || '').toLowerCase()));

            return JSON.stringify(res);
        }
        """);

            return JsonSerializer.Deserialize<CookieBannerInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new CookieBannerInfo();
        }
    }
}
