using Microsoft.Playwright;
using PdnAudit.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services {
    public class SiteScanner {
        private IPage page;
        private IBrowser browser;
        private IPlaywright playwright;
        public int linksAmount { get; set; } 
        public async Task<ScanResult> ScanSiteAsync() {
            ScanResult scanResult = new ScanResult();

            await StartBrowserAsync();

            scanResult.Links = await GetLinksAsync();
            //scanResult.Forms = await GetFormsAsync();

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
            var formsData = new List<FormInfo>();

            var formLocators = await page.Locator("form").AllAsync();
            foreach (var formLocator in formLocators)
            {


                var formInfoJson = await formLocator.EvaluateAsync<string>("""
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
    const submit = form.querySelector('button[type="submit"], input[type="submit"], button:not([type])');
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
""");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var formInfo = JsonSerializer.Deserialize<FormInfo>(formInfoJson, options);

                if (formInfo == null) continue;
                formInfo.BasePageUrl = page.Url;
                formsData.Add(formInfo);

            }
            return formsData;
        }
        
        public async Task StartBrowserAsync() {
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false
            });
            page = await browser.NewPageAsync();

            await page.GotoAsync("https://demoqa.com/automation-practice-form", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
            await page.WaitForTimeoutAsync(3000);
        }
        public async Task<(Document PrivacyDocument, Document ConsentDocument)> GetPdnDocuments() {
            var (privacyUrl, consentUrl) = await FindDocumentsLinks();

            Document privacyDocument = new Document();
            privacyDocument.Url = privacyUrl;
            privacyDocument.DocumentType = "Политика обработки персональных данных";

            Document consentDocument = new Document();
            consentDocument.Url = consentUrl;
            consentDocument.DocumentType = "Согласие на обработку персональных данных";

            return (privacyDocument, consentDocument);
        }
        private async Task<(string? PrivacyUrl, string? ConsentUrl)> FindDocumentsLinks() {
            var privacyRegex = new Regex(
    @"(" +
        @"политик[аиуеой]\s+(?:в\s+отношении\s+)?(?:конфиденциальност|обработк\w*\s+(?:персональн\w*\s+)?данн|персональн\w*\s+данн|защит\w*\s+(?:персональн\w*\s+)?данн)" +
        @"|конфиденциальност\w*\s+(?:персональн\w*\s+)?данн" +
        @"|обработк\w*\s+персональн\w*\s+данн" +
        @"|privacy\s+(?:policy|notice|statement)" +
        @"|personal\s+data\s+(?:processing|protection)\s+(?:policy|notice)" +
        @"|data\s+processing\s+(?:policy|notice)" +
        @"|политика\s+конфиденциальности" +
    @")",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);
            // Согласие на обработку ПДн
            var consentRegex = new Regex(
    @"(?:" +
        // 1) "согласие/согласия/согласию/согласием/согласии" + (на) + обработк/использовани/передач + данные/пдн
        @"согласи\w*\s+(?:на\s+)?(?:обработк\w*|использовани\w*|передач\w*)\s+(?:\w+\s+){0,4}(?:данн\w*|пдн|персональн\w*\s+данн)" +
        @"|согласи\w*\s+на\s+обработк\w*\s+персональн\w*\s+данн" +
        // 2) "я согласен/согласна на обработку"
        @"|я\s+соглас\w*\s+на\s+обработк" +
        // 3) "дать/даю/предоставляю согласие"
        @"|(?:дать|даю|предостав\w*)\s+согласи" +
        // 4) "соглашение" — но НЕ "пользовательское/лицензионное/... соглашение"
        //    и только если рядом (в пределах 0-4 слов) есть "обработк/пдн/данн/конфиденциальн"
        @"|(?<!пользовательск\w*\s)(?<!лицензионн\w*\s)(?<!публичн\w*\s)(?<!оферт\w*\s)" +
          @"соглашени\w*\s+(?:\w+\s+){0,4}(?:обработк\w*|пдн|персональн\w*\s+данн|конфиденциальн\w*)" +
        // 5) английские варианты
        @"|(?:data|personal\s+data|pdn|gdpr)\s+consent" +
        @"|consent\s+(?:to|for)\s+(?:the\s+)?(?:processing|use|transfer)\s+of\s+(?:personal\s+)?data" +
        @"|i\s+(?:hereby\s+)?consent\s+to\s+(?:the\s+)?processing" +
        // 6) "соглашение об обработке персональных данных" (частый случай)
        @"|соглашени\w*\s+об\s+обработк\w*\s+персональн\w*\s+данн" +
    @")",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

            string? privacyUrl = null;
            string? consentUrl = null;

            var baseUrl = new Uri(page.Url);

            var allLinks = await page.GetByRole(AriaRole.Link).AllAsync();

            foreach(var link in allLinks)
            {
                if (privacyUrl != null && consentUrl != null) break;
                
                string text = TextNormalizer.Normalize(await link.TextContentAsync());
                string href = TextNormalizer.Normalize(await link.GetAttributeAsync("href"));

                if (consentUrl == null && (consentRegex.IsMatch(text) || consentRegex.IsMatch(href)))
                {
                    consentUrl = new Uri(baseUrl, href).AbsoluteUri;
                    continue;
                }
                if (privacyUrl == null && (privacyRegex.IsMatch(text) || privacyRegex.IsMatch(href)))
                {
                    privacyUrl = new Uri(baseUrl, href).AbsoluteUri;
                    continue;
                }
               
            }
            if(privacyUrl == null || consentUrl == null)
            {
                List<string> linksByPriority = await FilterAndSortLinksByPriority();

                foreach (var link in linksByPriority)
                {
                    await page.GotoAsync(link);
                    await page.WaitForTimeoutAsync(1000);
                    var innerLinks = await page.GetByRole(AriaRole.Link).AllAsync();
                    foreach (var innerLink in innerLinks)
                    {
                        string text = TextNormalizer.Normalize(await innerLink.TextContentAsync());
                        string href = TextNormalizer.Normalize(await innerLink.GetAttributeAsync("href"));
                        if (string.IsNullOrEmpty(href)) continue;

                        if (consentUrl == null && consentRegex.IsMatch(text))
                            consentUrl = new Uri(new Uri(page.Url), href).AbsoluteUri;

                        if (privacyUrl == null && privacyRegex.IsMatch(text))
                            privacyUrl = new Uri(new Uri(page.Url), href).AbsoluteUri;
                    }

                    // Если нашли оба документа — дальше не идем
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
        
    }
}
