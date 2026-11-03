using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PdnAudit.Core.Models;

namespace PdnAudit.Core.Services {
    public class SiteScanner {
        private IPage page;
        private IBrowser browser;
        private IPlaywright playwright;
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
        private async Task StartBrowserAsync() {
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false
            });
            page = await browser.NewPageAsync();

            await page.GotoAsync("https://inf-oge.sdamgia.ru/", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
            await page.WaitForTimeoutAsync(3000);
        }
    }
}
