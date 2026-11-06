using Microsoft.Playwright;
using PdnAudit.Core.Models.Operator;
using PdnAudit.Core.Models.Scanning;
using PdnAudit.Core.Models.Tracking;
using PdnAudit.Core.Services.Dictionaries;
using PdnAudit.Core.Services.Extractors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services.Analyzers {
    public class SiteScanner {
        private IPage page;
        private IBrowser browser;
        private IPlaywright playwright;
        private int maxExtraPages = 6;
        private string _startUrl;
        public int linksAmount { get; set; }
        private const string FormScript = """
(form) => {
    const norm = (s) => (s || '')
        .replace(/[\u00AD\u200B-\u200D\uFEFF]/g, '')
        .replace(/\u00A0/g, ' ')
        .replace(/\s+/g, ' ')
        .trim()
        .toLowerCase();

    const result = {
        ActionUrl: '',
        FormType: '',
        HasConsentCheckbox: false,
        ConsentCheckedByDefault: false,
        ConsentText: '',
        NearbyPrivacyLink: false,
        NearbyConsentLink: false,
        HasImplicitConsentText: false,
        SubmitTextNearby: '',
        Fields: []
    };

    // ---------- action ----------
    try {
        const raw = form.getAttribute('action');
        result.ActionUrl = raw ? new URL(raw, document.baseURI).href : document.location.href;
    } catch (e) {
        result.ActionUrl = document.location.href;
    }

    // ---------- области "рядом с формой": форма -> родитель -> родитель родителя ----------
    const scopes = [form];
    let p = form.parentElement;
    for (let i = 0; i < 2 && p; i++) {
        const tag = p.tagName.toLowerCase();
        if (tag === 'body' || tag === 'main' || tag === 'html') break;
        scopes.push(p);
        p = p.parentElement;
    }

    // ---------- подпись элемента ----------
    const getLabel = (el) => {
        if (el.labels && el.labels.length > 0) return norm(el.labels[0].innerText);
        const wrap = el.closest('label');
        if (wrap) return norm(wrap.innerText);
        const aria = el.getAttribute('aria-label');
        if (aria) return norm(aria);
        const par = el.parentElement;
        if (par) {
            const t = norm(par.innerText);
            return t.length <= 150 ? t : '';
        }
        return '';
    };

    // ---------- 1. поля ввода (ПДн-релевантные) ----------
    const skipTypes = ['hidden', 'submit', 'button', 'reset', 'image', 'checkbox',
                       'radio', 'file', 'search'];
    form.querySelectorAll('input, textarea').forEach(el => {
        const type = (el.tagName.toLowerCase() === 'textarea') ? 'textarea' : (el.type || 'text');
        if (skipTypes.includes(type)) return;
        // пропускаем служебные поля без имени, подписи и подсказки (например, внутренние поля выпадающих списков)
        const label = getLabel(el);
        const name = el.name || el.id || '';
        const ph = norm(el.placeholder);
        if (!name && !ph && !label) return;
        result.Fields.push({
            Type: type,
            Name: name,
            Label: label,
            Placeholder: ph
        });
    });

    // ---------- 2. чекбокс согласия ----------
    const consentWords = /соглас|персональн|обработк|конфиденциальн|privacy|consent|policy|политик/;
    form.querySelectorAll('input[type="checkbox"]').forEach(cb => {
        const text = getLabel(cb);
        if (consentWords.test(text) && !result.HasConsentCheckbox) {
            result.HasConsentCheckbox = true;
            result.ConsentCheckedByDefault = cb.checked;
            result.ConsentText = text;
        }
    });

    // ---------- 3. ссылки на политику / согласие в области рядом с формой ----------
    const privacyWords = /политик|конфиденциальн|персональн|privacy|policy|pdn/;
    const consentLinkWords = /соглас|consent|agreement/;
    for (const scope of scopes) {
        scope.querySelectorAll('a[href]').forEach(a => {
            const text = norm(a.innerText || a.textContent);
            const href = (a.getAttribute('href') || '').toLowerCase();
            if (privacyWords.test(text) || privacyWords.test(href)) result.NearbyPrivacyLink = true;
            if (consentLinkWords.test(text) || consentLinkWords.test(href)) result.NearbyConsentLink = true;
        });
        if (result.NearbyPrivacyLink && result.NearbyConsentLink) break;
    }

    // ---------- 4. неявное согласие ("нажимая кнопку, вы соглашаетесь...") ----------
    const implicitRe = /(нажимая|нажав|отправляя|оставляя|регистрируясь|подтверждая).{0,100}(соглас|принима|подтвержда)/;
    for (const scope of scopes) {
        if (implicitRe.test(norm(scope.innerText))) {
            result.HasImplicitConsentText = true;
            break;
        }
    }

    // ---------- 5. текст кнопки отправки ----------
    const submit = form.querySelector('button[type="submit"], input[type="submit"], button:not([type]), [role=button]');
    if (submit) result.SubmitTextNearby = norm(submit.innerText || submit.value || '');

    // ---------- 6. грубое определение типа формы ----------
    const types = result.Fields.map(f => f.Type);
    const allText = result.Fields.map(f => `${f.Name} ${f.Label} ${f.Placeholder}`).join(' ');
    const hasPassword = types.includes('password');
    if (hasPassword && result.Fields.length <= 2) result.FormType = 'Авторизация';
    else if (hasPassword) result.FormType = 'Регистрация';
    else if (/адрес|address|доставк|delivery|оплат/.test(allText)) result.FormType = 'Оформление заказа';
    else if (/сообщен|message|обратн|заявк|звонок|comment|вопрос/.test(allText) || types.includes('textarea'))
        result.FormType = 'Обратная связь / заявка';
    else if (result.Fields.length === 0) result.FormType = 'Без полей';
    else result.FormType = 'Прочая форма';

    return JSON.stringify(result);
}
""";
        private record LinkDto(string T, string H);
        private static readonly (string Kind, Regex Rx)[] FormPageKinds =
{
    ("Обратная связь", new Regex(@"контакт|обратн\w*\s+связ|связаться|заявк|заказать|звонок|feedback|contact|callback|request",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ("Авторизация", new Regex(@"\bвойти\b|\bвход\b|авториз|login|log-in|signin|sign-in|\bauth\b|личн\w*\s+кабинет|account",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ("Регистрация", new Regex(@"регистрац|зарегистр|register|signup|sign-up",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ("Заказ", new Regex(@"корзин|оформлен|cart|basket|checkout|order",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ("Подписка", new Regex(@"подпис|subscribe|newsletter",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)),
};

        private static readonly Regex FileExtRx = new(@"\.(jpe?g|png|gif|svg|webp|pdf|docx?|xlsx?|zip|rar|mp4|mp3)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
        public async Task<ScanResult> ScanSiteAsync(string startUrl, IProgress<ScanProgress> progress = null) {
            progress?.Report(new ScanProgress("Запуск браузера..."));

            _startUrl = startUrl;

            ScanResult scanResult = new ScanResult { Url = startUrl };

            await StartBrowserAsync();

            scanResult.PageTitle = await page.TitleAsync();
            scanResult.Links = await GetLinksAsync();

            progress?.Report(new ScanProgress("Поиск и анализ форм на сайте..."));
            scanResult.Forms = await GetFormsAsync();
            scanResult.NeedsManualCheck = scanResult.Forms.Count == 0;

            return scanResult;
        }
        
        private async Task<List<string>> GetLinksAsync() {
            if (page == null) return new List<string>();

            var linkLocators = await page.GetByRole(AriaRole.Link).AllAsync();
            var links = new List<string>();

            foreach (var locator in linkLocators)
            {
                var href = await locator.GetAttributeAsync("href");
                if (!string.IsNullOrEmpty(href))
                {
                    links.Add(href);
                }
            }
            return links;
        }
        public async Task<List<FormInfo>> GetFormsAsync() {
            var all = new List<FormInfo>();
            var seen = new HashSet<string>();

            void AddUnique(IEnumerable<FormInfo> forms) {
                foreach (var f in forms)
                {
                    string sig = $"{f.FormType}|{f.ActionUrl}|" +
                                 string.Join(",", f.Fields.Select(x => $"{x.Type}:{x.Name}:{x.Placeholder}"));
                    if (seen.Add(sig)) all.Add(f);
                }
            }

            // 1. Стартовая страница
            List<string> extraUrls;

            var startContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            });
            var start = await startContext.NewPageAsync();

            try
            {
                await OpenForScanAsync(start, _startUrl);
                AddUnique(await ExtractFormsAsync(start));
                extraUrls = await FindFormPagesAsync(start, maxExtraPages);
            }
            finally
            {
                await startContext.CloseAsync(); // Закрываем контекст вместе со страницей
            }

            // 2. Остальные страницы
            foreach (var url in extraUrls)
            {
                
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                });
                var p = await context.NewPageAsync();

                try
                {
                    await OpenForScanAsync(p, url);
                    AddUnique(await ExtractFormsAsync(p));
                }
                catch (PlaywrightException ex)
                {
                    Debug.WriteLine($"FORMS: не открыть {url}: {ex.Message}");
                }
                finally
                {
                    await context.CloseAsync(); // Обязательно закрываем контекст
                }
            }

            // 3. Формы без полей ввода (поиск и т.п.) не относятся к сбору ПДн
            return all.Where(f => f.Fields.Count > 0).ToList();
        }
        // ---- открыть страницу и подготовить к сбору ----
        private static async Task OpenForScanAsync(IPage p, string url) {
            await p.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await p.WaitForTimeoutAsync(1500);
            await p.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
            await p.WaitForTimeoutAsync(1000);
        }

        // ---- все формы одной страницы ----
        private async Task<List<FormInfo>> ExtractFormsAsync(IPage p) {
            var result = new List<FormInfo>();
            var locators = await p.Locator("form").AllAsync();

            foreach (var formLocator in locators.Take(15))   // защита от страниц с сотнями форм (каталог)
            {
                try
                {
                    string? json = await formLocator.EvaluateAsync<string>(FormScript);
                    if (string.IsNullOrEmpty(json)) continue;

                    var info = JsonSerializer.Deserialize<FormInfo>(json, JsonOpts);
                    if (info == null) continue;

                    info.BasePageUrl = p.Url;
                    result.Add(info);
                }
                catch (Exception ex) when (ex is PlaywrightException || ex is JsonException)
                {
                    Debug.WriteLine($"FORMS: ошибка формы на {p.Url}: {ex.Message}");
                }
            }
            return result;
        }

        // ---- поиск страниц, где вероятны формы ----
        private async Task<List<string>> FindFormPagesAsync(IPage p, int maxPages) {
            string json = await p.EvaluateAsync<string>("""
        () => JSON.stringify([...document.querySelectorAll('a[href]')]
              .map(a => ({ t: a.innerText || a.textContent || '', h: a.href })))
        """);

            var links = JsonSerializer.Deserialize<List<LinkDto>>(json, JsonOpts) ?? new();
            var startUri = new Uri(_startUrl);
            var perKind = new Dictionary<string, int>();
            var result = new List<string>();

            foreach (var l in links)
            {
                if (result.Count >= maxPages) break;
                if (!Uri.TryCreate(l.H, UriKind.Absolute, out var u)) continue;
                if (u.Scheme != "http" && u.Scheme != "https") continue;
                if (!TrackerDictionary.IsSameSite(u.Host, startUri.Host)) continue;   // только свой сайт

                string clean = u.GetLeftPart(UriPartial.Path);
                if (FileExtRx.IsMatch(clean)) continue;
                if (result.Contains(clean)) continue;
                if (clean.TrimEnd('/') == _startUrl.TrimEnd('/')) continue;           // стартовую уже разобрали

                string text = TextNormalizer.Normalize(l.T);
                string path = Uri.UnescapeDataString(u.AbsolutePath).ToLowerInvariant();

                foreach (var (kind, rx) in FormPageKinds)
                {
                    if (!rx.IsMatch(text) && !rx.IsMatch(path)) continue;

                    perKind.TryGetValue(kind, out int n);
                    if (n >= 2) break;                  // не больше двух страниц каждого вида

                    perKind[kind] = n + 1;
                    result.Add(clean);
                    break;
                }
            }
            return result;
        }
        
        public async Task StartBrowserAsync() {
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            });
            page = await context.NewPageAsync();

            await page.GotoAsync(_startUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });
            await page.WaitForTimeoutAsync(4000);

            await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
            await page.WaitForTimeoutAsync(2000);
        }

        public async Task<(Document PrivacyDocument, Document ConsentDocument)> GetPdnDocuments(IProgress<ScanProgress> progress = null) {
            progress?.Report(new ScanProgress("Поиск ссылки на политику обработки данных"));
            var (privacyUrl, consentUrl) = await FindDocumentsLinks();

            Document privacyDocument = new Document();
            privacyDocument.Url = privacyUrl;
            privacyDocument.DocumentType = "Политика обработки персональных данных";

            // Если ссылка найдена, сразу скачиваем текст
            if (!string.IsNullOrEmpty(privacyUrl))
            {
                progress?.Report(new ScanProgress("Скачивание текста политики обработки данных"));
                privacyDocument.FullText = await GetTextFromUrlAsync(privacyUrl);
            }

            Document consentDocument = new Document();
            consentDocument.Url = consentUrl;
            consentDocument.DocumentType = "Согласие на обработку персональных данных";

            // Если ссылка найдена, сразу скачиваем текст
            if (!string.IsNullOrEmpty(consentUrl))
            {
                progress?.Report(new ScanProgress("Скачивание текста согласия на обработку данных"));
                consentDocument.FullText = await GetTextFromUrlAsync(consentUrl);
            }

            return (privacyDocument, consentDocument);
        }
        private async Task<string> GetTextFromUrlAsync(string url) {
            // Создаем изолированный контекст, чтобы не влиять на основную страницу page
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            });
            var p = await context.NewPageAsync();

            try
            {
                // Переходим по ссылке
                await p.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Commit, // Ждем начала загрузки, чтобы не зависнуть
                    Timeout = 30000
                });

                // Ждем, чтобы текст прогрузился (особенно если он рендерится через JS)
                await p.WaitForTimeoutAsync(2000);

                // Забираем весь видимый текст
                string text = await p.Locator("body").InnerTextAsync();
                return text;
            }
            catch (PlaywrightException ex)
            {
                Debug.WriteLine($"Ошибка загрузки текста по ссылке {url}: {ex.Message}");
                return string.Empty; // Если не удалось, возвращаем пустоту
            }
            finally
            {
                // Обязательно закрываем контекст, чтобы не копить память
                await context.CloseAsync();
            }
        }
        private async Task<(string? PrivacyUrl, string? ConsentUrl)> FindDocumentsLinks() {
            var privacyRegex = new Regex(
                @"политик[аиуеой]\s+(?:в\s+отношении\s+)?(?:конфиденциальност|обработк\w*\s+(?:персональн\w*\s+)?данн|персональн\w*\s+данн|защит\w*\s+(?:персональн\w*\s+)?данн)" +
                @"|конфиденциальност\w*\s+(?:персональн\w*\s+)?данн|обработк\w*\s+персональн\w*\s+данн" +
                @"|privacy\s+(?:policy|notice|statement)|personal\s+data\s+(?:processing|protection)\s+(?:policy|notice)" +
                @"|data\s+processing\s+(?:policy|notice)|политика\s+конфиденциальности", RegexOptions.IgnoreCase);

            var consentRegex = new Regex(
                @"согласи\w*\s+(?:на\s+)?(?:обработк\w*|использовани\w*|передач\w*)\s+(?:\w+\s+){0,4}(?:данн\w*|пдн|персональн\w*\s+данн)" +
                @"|согласи\w*\s+на\s+обработк\w*\s+персональн\w*\s+данн|я\s+соглас\w*\s+на\s+обработк" +
                @"|(?:дать|даю|предостав\w*)\s+согласи" +
                @"|(?<!пользовательск\w*\s)(?<!лицензионн\w*\s)(?<!публичн\w*\s)(?<!оферт\w*\s)соглашени\w*\s+(?:\w+\s+){0,4}(?:обработк\w*|пдн|персональн\w*\s+данн|конфиденциальн\w*)" +
                @"|(?:data|personal\s+data|pdn|gdpr)\s+consent|consent\s+(?:to|for)\s+(?:the\s+)?(?:processing|use|transfer)\s+of\s+(?:personal\s+)?data|i\s+(?:hereby\s+)?consent\s+to\s+(?:the\s+)?processing" +
                @"|соглашени\w*\s+об\s+обработк\w*\s+персональн\w*\s+данн", RegexOptions.IgnoreCase);

            string? privacyUrl = null;
            string? consentUrl = null;

            // Получаем все ссылки ОДНИМ вызовом (через JS), чтобы не дергать DOM в цикле
            string json = await page.EvaluateAsync<string>("""
                () => JSON.stringify([...document.querySelectorAll('a[href]')]
                      .map(a => ({ t: a.innerText || '', h: a.href })))
            """);
            var allLinks = JsonSerializer.Deserialize<List<LinkDto>>(json, JsonOpts) ?? new();
            var baseUri = new Uri(_startUrl); // Берем стартовый URL, а не page.Url!

            foreach (var link in allLinks)
            {
                if (privacyUrl != null && consentUrl != null) break;

                // Текст нормализуем, а href оставляем как есть (чтобы не сломать регистр)
                string text = TextNormalizer.Normalize(link.T);
                string href = link.H;
                if (string.IsNullOrEmpty(href) || href.StartsWith("javascript:")) continue;

                // Проверяем только свой домен
                if (!Uri.TryCreate(href, UriKind.Absolute, out var linkUri)) continue;
                if (!linkUri.Host.Contains(baseUri.Host)) continue;

                // Ищем согласие
                if (consentUrl == null && (consentRegex.IsMatch(text) || consentRegex.IsMatch(href)))
                {
                    consentUrl = linkUri.AbsoluteUri;
                    continue;
                }
                // Ищем политику
                if (privacyUrl == null && (privacyRegex.IsMatch(text) || privacyRegex.IsMatch(href)))
                {
                    privacyUrl = linkUri.AbsoluteUri;
                    continue;
                }
            }

            // Запасной обход 
            if (privacyUrl == null || consentUrl == null)
            {
                List<string> linksByPriority = await FilterAndSortLinksByPriority();

                foreach (var link in linksByPriority)
                {
                    // Открываем в изолированном контексте, чтобы не ломать основной page
                    var context = await browser.NewContextAsync(new BrowserNewContextOptions
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
                    });
                    var p = await context.NewPageAsync();
                    try
                    {
                        await p.GotoAsync(link, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 20000 });
                        await p.WaitForTimeoutAsync(1000);

                        string innerJson = await p.EvaluateAsync<string>("""
                            () => JSON.stringify([...document.querySelectorAll('a[href]')]
                                  .map(a => ({ t: a.innerText || '', h: a.href })))
                        """);
                        var innerLinks = JsonSerializer.Deserialize<List<LinkDto>>(innerJson, JsonOpts) ?? new();

                        foreach (var innerLink in innerLinks)
                        {
                            string text = TextNormalizer.Normalize(innerLink.T);
                            string href = innerLink.H;
                            if (string.IsNullOrEmpty(href) || !Uri.TryCreate(href, UriKind.Absolute, out var uri)) continue;
                            if (!uri.Host.Contains(baseUri.Host)) continue; 

                            if (consentUrl == null && consentRegex.IsMatch(text))
                                consentUrl = uri.AbsoluteUri;
                            if (privacyUrl == null && privacyRegex.IsMatch(text))
                                privacyUrl = uri.AbsoluteUri;
                        }
                    }
                    catch (PlaywrightException) { /* Игнорируем ошибки загрузки */ }
                    finally { await context.CloseAsync(); }

                    if (privacyUrl != null && consentUrl != null) break;
                }
            }
            return (privacyUrl, consentUrl);
        }
        private async Task<List<string>> FilterAndSortLinksByPriority() {
            var prioritylinks = new List<(string link, int score)>();

            var allLinks = await page.GetByRole(AriaRole.Link).AllAsync();

            var baseUrl = new Uri(page.Url);

            string highPattern = @"политик|обработк|персональн|соглашен|документ|конфиденциальн|лиценз";
            string mediumPattern = @"контакт|о\sкомпании|о\sнас|реквизит|оферт|регистрац|вход|заявк|прав";
            string junkPattern = @"\.(jpg|png|zip|pdf|docx)$|^mailto:|^tel:|^#";

            foreach (var link in allLinks)
            {
                string text = await link.TextContentAsync() ?? "";
                string href = await link.GetAttributeAsync("href") ?? "";
                int score = 0;

                if (Regex.IsMatch(text, junkPattern, RegexOptions.IgnoreCase) || Regex.IsMatch(href, junkPattern, RegexOptions.IgnoreCase)) continue;

                if (Regex.IsMatch(text, highPattern, RegexOptions.IgnoreCase) || Regex.IsMatch(href, highPattern, RegexOptions.IgnoreCase)) score = 100;
                else if (Regex.IsMatch(text, mediumPattern, RegexOptions.IgnoreCase) || Regex.IsMatch(href, mediumPattern, RegexOptions.IgnoreCase)) score = 50;

                if(score > 0)
                {
                    prioritylinks.Add((new Uri(baseUrl,href).AbsoluteUri, score));
                }
            }
            linksAmount = prioritylinks.Count;
            return prioritylinks
        .GroupBy(x => x.link)
        .Select(g => g.First())
        .OrderByDescending(x => x.score)
        .Select(x => x.link)
        .ToList();
        }

        public async Task<OperatorInfo> GetOperatorInfoAsync(string? policyUrl, IProgress<ScanProgress> progress = null) {
            progress?.Report(new ScanProgress("Поиск реквизитов оператора..."));
            var info = new OperatorInfo();
            var start = new Uri(_startUrl);

            await ExtractOperatorFromPageAsync(page, info);

            var urls = await FindContactPagesAsync(start);
            if (!string.IsNullOrEmpty(policyUrl)) urls.Add(policyUrl);

            foreach (var url in urls.Distinct().Take(4))
            {
                var newContext = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
                });
                var p = await newContext.NewPageAsync();
                try
                {
                    await p.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
                    await p.WaitForTimeoutAsync(1000);
                    await ExtractOperatorFromPageAsync(p, info);
                }
                catch (PlaywrightException ex)
                {
                    Debug.WriteLine($"CONTACTS: страница {url} не открылась: {ex.Message}");
                }
                finally
                {
                    await newContext.CloseAsync();
                }
            }
            return info;
        }

        private async Task ExtractOperatorFromPageAsync(IPage p, OperatorInfo info) {
            await p.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
            await p.WaitForTimeoutAsync(500);

            string footer = string.Join("\n", await p.Locator("footer").AllInnerTextsAsync());
            OperatorInfoExtractor.Extract(footer, p.Url, info);

            string body = await p.InnerTextAsync("body");
            OperatorInfoExtractor.Extract(body, p.Url, info);

            // mailto: и tel: ссылки
            var hrefs = await p.EvaluateAsync<string[]>("""
        () => [...document.querySelectorAll('a[href^="mailto:"], a[href^="tel:"]')]
              .map(a => a.getAttribute('href'))
        """);
            foreach (var h in hrefs ?? Array.Empty<string>())
            {
                if (info.Email == null && h.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                { info.Email = h[7..].Split('?')[0]; info.Sources["Email"] = p.Url; }
                if (info.Phone == null && h.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
                { info.Phone = h[4..]; info.Sources["Телефон"] = p.Url; }
            }
        }

        private async Task<List<string>> FindContactPagesAsync(Uri start) {
            var result = new List<string>();
            string json = "";

            // 1. Оборачиваем EvaluateAsync в try-catch, чтобы программа не висла на мертвой странице
            try
            {
                json = await page.EvaluateAsync<string>("""
            () => JSON.stringify([...document.querySelectorAll('a[href]')]
                  .map(a => ({ t: a.innerText || '', h: a.href })))
            """);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Не удалось получить ссылки для контактов: {ex.Message}");
                return result; // Возвращаем пустой список, идем дальше по коду
            }

            var links = JsonSerializer.Deserialize<List<LinkDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            var rx = new Regex(@"контакт|реквизит|о\s+компании|о\s+нас|about|contact|requisite", RegexOptions.IgnoreCase);

            foreach (var l in links)
            {
                if (!Uri.TryCreate(l.H, UriKind.Absolute, out var u)) continue;
                if (!TrackerDictionary.IsSameSite(u.Host, start.Host)) continue;
                if (rx.IsMatch(TextNormalizer.Normalize(l.T)) || rx.IsMatch(u.AbsolutePath))
                    result.Add(u.GetLeftPart(UriPartial.Path));
            }

            return result.Distinct().Take(3).ToList();
        }
        public async Task CloseAsync() {
            if (browser != null) await browser.CloseAsync();
            if (playwright != null) playwright.Dispose();
        }
        public async Task<TrackingResult> GetTrackingAsync(string url, IProgress<ScanProgress> progress = null) {
            progress?.Report(new ScanProgress("Проверка трекеров и cookie..."));
            return await new TrackingAnalyzer(browser).AnalyzeAsync(url);
        } 
    }
}
//https://www.citilink.ru/ https://demoqa.com/automation-practice-form
// https://www.techport.ru/?srsltid=AYCX9vKJ-rCbGCqVORI4TYIGUg5Y2n5JXPXAnjE2ulZcu-qc5EPqvztZ