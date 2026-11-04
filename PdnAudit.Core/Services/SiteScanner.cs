using Microsoft.Playwright;
using PdnAudit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            scanResult.Forms = await GetFormsAsync();

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
        private async Task<List<string>> GetFormsAsync() {
            if (page == null) return new List<string>();

            var formsLocators = await page.Locator("form").AllAsync();
            var formsId = new List<string>();

            foreach (var form in formsLocators)
            {
                var id = await form.GetAttributeAsync("id") ?? "no id";
                formsId.Add(id);
            }
            return formsId;
        }
        public async Task StartBrowserAsync() {
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false
            });
            page = await browser.NewPageAsync();

            await page.GotoAsync("https://www.citilink.ru/", new PageGotoOptions
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
