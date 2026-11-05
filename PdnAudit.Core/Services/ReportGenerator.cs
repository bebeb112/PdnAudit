using Microsoft.Playwright;
using PdnAudit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services {
    public class ReportGenerator {
        public async Task GeneratePdfReportAsync(List<CheckResult> results, string siteUrl, string outputPath) {
            // 1. Генерируем HTML-код отчета
            string html = BuildHtml(results, siteUrl);

            // 2. Сохраняем HTML во временный файл (Playwright не принимает чистый HTML напрямую)
            string tempHtmlPath = Path.GetTempFileName() + ".html";
            await File.WriteAllTextAsync(tempHtmlPath, html, Encoding.UTF8);

            // 3. Запускаем Playwright для конвертации в PDF
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();

            // Открываем локальный HTML-файл
            await page.GotoAsync($"file:///{tempHtmlPath.Replace("\\", "/")}");

            // Сохраняем как PDF
            await page.PdfAsync(new PagePdfOptions
            {
                Path = outputPath,
                Format = "A4",
                PrintBackground = true,
                Margin = new Margin { Top = "20px", Bottom = "20px", Left = "20px", Right = "20px" }
            });

            // Удаляем временный файл
            File.Delete(tempHtmlPath);
        }

        private string BuildHtml(List<CheckResult> results, string siteUrl) {
            var critical = results.Where(r => r.Severity == Severity.Critical).ToList();
            var warnings = results.Where(r => r.Severity == Severity.Warning || r.Severity == Severity.Info).ToList();
            var passed = results.Where(r => r.Severity == Severity.Passed).ToList();

            var sb = new StringBuilder();
            sb.Append($@"
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #333; }}
        h1 {{ color: #2c3e50; border-bottom: 2px solid #2c3e50; padding-bottom: 10px; }}
        h2 {{ margin-top: 30px; }}
        .critical-title {{ color: #D32F2F; }}
        .warning-title {{ color: #F57C00; }}
        .passed-title {{ color: #388E3C; }}
        .item {{ margin-bottom: 15px; padding: 15px; border-left: 5px solid; page-break-inside: avoid; }}
        .item-critical {{ background-color: #FFEBEE; border-color: #D32F2F; }}
        .item-warning {{ background-color: #FFF3E0; border-color: #F57C00; }}
        .item-passed {{ background-color: #E8F5E9; border-color: #388E3C; }}
        .item-title {{ font-size: 16px; font-weight: bold; margin-bottom: 5px; }}
        .item-law {{ font-size: 14px; color: #555; font-style: italic; margin-bottom: 5px; }}
        .item-rec {{ font-size: 14px; background: rgba(255,255,255,0.6); padding: 8px; border-radius: 4px; margin-top: 10px; }}
        .header-info {{ background: #f4f4f4; padding: 15px; border-radius: 5px; margin-bottom: 30px; }}
    </style>
</head>
<body>
    <h1>Отчет о соответствии 152-ФЗ</h1>
    <div class='header-info'>
        <p><strong>Проверенный сайт:</strong> {siteUrl}</p>
        <p><strong>Дата проверки:</strong> {System.DateTime.Now:dd.MM.yyyy HH:mm}</p>
        <p><strong>Статистика:</strong> Критично: {critical.Count} | Замечаний: {warnings.Count} | Пройдено: {passed.Count}</p>
    </div>
");

            sb.Append("<h2 class='critical-title'>Критические нарушения</h2>");
            if (!critical.Any()) sb.Append("<p>Критических нарушений не выявлено.</p>");
            foreach (var item in critical)
                sb.Append(BuildItemHtml(item, "critical"));

            sb.Append("<h2 class='warning-title'>Замечания</h2>");
            if (!warnings.Any()) sb.Append("<p>Замечаний не выявлено.</p>");
            foreach (var item in warnings)
                sb.Append(BuildItemHtml(item, "warning"));

            sb.Append("<h2 class='passed-title'>Успешно пройдено</h2>");
            if (!passed.Any()) sb.Append("<p>Нет данных.</p>");
            foreach (var item in passed)
                sb.Append(BuildItemHtml(item, "passed"));

            sb.Append("</body></html>");
            return sb.ToString();
        }

        private string BuildItemHtml(CheckResult item, string cssClass) {
            string recHtml = string.IsNullOrEmpty(item.Recommendation) ? "" : $"<div class='item-rec'><strong>Рекомендация:</strong> {item.Recommendation}</div>";
            string lawHtml = string.IsNullOrEmpty(item.LawReference) ? "" : $"<div class='item-law'><strong>Норма закона:</strong> {item.LawReference}</div>";
            string detHtml = string.IsNullOrEmpty(item.Details) ? "" : $"<div style='font-size:12px; color:#777; margin-top:5px;'>{item.Details}</div>";

            return $@"
<div class='item item-{cssClass}'>
    <div class='item-title'>{item.Title}</div>
    {lawHtml}
    {recHtml}
    {detHtml}
</div>";
        }
    }
}
