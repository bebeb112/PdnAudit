using Microsoft.Playwright;
using PdnAudit.Core.Models;
using PdnAudit.Core.Services;
using PdnAudit.Core.Services.Extractors;
using PdnAudit.Core.Services.Rules;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace PdnAudit
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IPage page;
        private IBrowser browser;
        private IPlaywright playwright;
        public MainWindow() {
            InitializeComponent();

            AIAnalyzer analyzer = new AIAnalyzer();
            Loaded += MainWindow_Loaded;
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            DocumentLinksAmountTextBlock.Text = "";
            FormsAmountTextBlock.Text = "";
            ResultsTextBlock.Text = "";

            SiteScanner scanner = null;

            try
            {
                // 1. Инициализация сканера и запуск браузера
                scanner = new SiteScanner();
                ScanResult scanResult = await scanner.ScanSiteAsync();

                Uri baseUrl = new Uri("https://www.citilink.ru/");

                // 2. Поиск ссылок на Политику и Согласие
                (Document privacyDocument, Document consentDocument) = await scanner.GetPdnDocuments();

                FormsAmountTextBlock.Text += $"Найдено форм: {scanResult.Forms.Count}\n";
                if (scanResult.NeedsManualCheck)
                    FormsAmountTextBlock.Text += "ВНИМАНИЕ: Форма не найдена автоматически, нужна ручная проверка\n";

                // 3. Анализ трекеров и Cookies
                var tracking = await scanner.GetTrackingAsync(baseUrl.ToString());

                // 4. Поиск реквизитов
                var op = await scanner.GetOperatorInfoAsync(privacyDocument?.Url);

                // 5. Сбор всех правил в один список
                var all = new List<CheckResult>();

                if (scanResult.Forms != null)
                    all.AddRange(FormRules.CheckForms(scanResult.Forms));

                all.AddRange(OperatorRules.Check(op));
                all.AddRange(TrackingRules.Check(tracking));

                // Проверка наличия самих документов
                if (string.IsNullOrEmpty(privacyDocument?.Url))
                    all.Add(new CheckResult { Severity = Severity.Critical, Title = "Не найдена Политика обработки ПДн" });

                if (string.IsNullOrEmpty(consentDocument?.Url))
                    all.Add(new CheckResult { Severity = Severity.Warning, Title = "Не найдено отдельное Согласие на обработку ПДн" });

                if (privacyDocument != null && !string.IsNullOrEmpty(privacyDocument.Url))
                {
                    try
                    {
                        AIAnalyzer analyzer = new AIAnalyzer();
                        string token = await analyzer.GetGigaChatTokenAsync();
     
                        // string policyText = "Настоящая политика определяет цели обработки данных: оформление заказов. Срок хранения данных: 5 лет. Передача третьим лицам не осуществляется."
                        if (privacyDocument != null && !string.IsNullOrEmpty(privacyDocument.FullText))
                        {
                            string rawAiResponse = await analyzer.AnalyzeGigaChatAsync(token, privacyDocument.FullText);

                            string cleanJson = TextNormalizer.CleanJsonFromAI(rawAiResponse);

                            AiAnalysisResult aiResult = analyzer.ParseAiResponse(cleanJson);

                            all.AddRange(AiAnalysisRules.Check(aiResult));
                        }
                        else
                        {
                            all.Add(new CheckResult { Severity = Severity.Info, Title = "Текст Политики слишком короткий для анализа ИИ" });
                        }
                    }
                    catch (Exception ex)
                    {
                        all.Add(new CheckResult { Severity = Severity.Info, Title = $"Ошибка при анализе ИИ: {ex.Message}" });
                    }
                }
                var critical = all.Where(r => r.Severity == Severity.Critical).ToList();
                var warnings = all.Where(r => r.Severity == Severity.Warning || r.Severity == Severity.Info).ToList();
                var passed = all.Where(r => r.Severity == Severity.Passed).ToList();

                var uniqueCritical = critical.GroupBy(c => c.Title).Select(g => g.First()).ToList();

                foreach (var c in uniqueCritical)
                {
                    DocumentLinksAmountTextBlock.Text += $"[КРИТИЧНО] {c.Title}\n";
                }

                foreach (var w in warnings)
                {
                    FormsAmountTextBlock.Text += $"[ЗАМЕЧАНИЕ] {w.Title}\n";
                }

                foreach (var p in passed)
                {
                    ResultsTextBlock.Text += $"[ОК] {p.Title}\n";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка при работе программы: {ex.Message}\n\n{ex.StackTrace}", "Ошибка");
            }
            finally
            {
                // 9. Обязательно закрываем браузер и освобождаем ресурсы
                if (scanner != null)
                {
                    await scanner.CloseAsync();
                }
            }
        }
    }
}