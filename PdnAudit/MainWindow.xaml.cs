using Microsoft.Playwright;
using Microsoft.Win32;
using PdnAudit.Core.Models;
using PdnAudit.Core.Models.Tracking;
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
        private List<CheckResult> _lastResults = new List<CheckResult>();
        public MainWindow() {
            InitializeComponent();
        }
        private async void CheckButton_Click(object sender, RoutedEventArgs e) {
            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Введите URL сайта", "Ошибка");
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            CheckButton.IsEnabled = false;

            var progress = new Progress<ScanProgress>(status =>
            {
                LoadingStatusText.Text = status.Message;
            });

            // Блокируем кнопку, чтобы не нажимали дважды
            CheckButton.IsEnabled = false;
            CheckButton.Content = "Анализ...";

            // Очищаем старые результаты
            CriticalList.ItemsSource = null;
            WarningList.ItemsSource = null;
            PassedList.ItemsSource = null;

            SiteScanner scanner = null;

            try
            {
                scanner = new SiteScanner();
                ScanResult scanResult = await scanner.ScanSiteAsync(url, progress);

                Uri baseUrl = new Uri(url);

                (Document privacyDocument, Document consentDocument) = await scanner.GetPdnDocuments(progress);
                var tracking = await scanner.GetTrackingAsync(baseUrl.ToString(), progress);
                var op = await scanner.GetOperatorInfoAsync(privacyDocument?.Url, progress);

                var all = new List<CheckResult>();

                if (scanResult.Forms != null)
                    all.AddRange(FormRules.CheckForms(scanResult.Forms));
                all.AddRange(OperatorRules.Check(op));
                all.AddRange(TrackingRules.Check(tracking));

                if (string.IsNullOrEmpty(privacyDocument?.Url))
                    all.Add(new CheckResult { Severity = Severity.Critical, Title = "Не найдена Политика обработки ПДн" });

                if (string.IsNullOrEmpty(consentDocument?.Url))
                    all.Add(new CheckResult { Severity = Severity.Warning, Title = "Не найдено отдельное Согласие на обработку ПДн" });

                if (privacyDocument != null && !string.IsNullOrEmpty(privacyDocument.FullText))
                {
                    try
                    {
                        LoadingStatusText.Text = "ИИ анализирует текст Политики обработки данных...";

                        AIAnalyzer analyzer = new AIAnalyzer();
                        string token = await analyzer.GetGigaChatTokenAsync();
                        string rawAiResponse = await analyzer.AnalyzeGigaChatAsync(token, privacyDocument.FullText);

                        var aiResponse = analyzer.ParseAndVerify(rawAiResponse, privacyDocument.FullText);
                        bool hasForeignTrackers = tracking.Before.Requests.Concat(tracking.After.Requests).Any(x => x.IsForeign && (x.Category == TrackerCategory.Analytics || x.Category == TrackerCategory.Ads));
                        
                        all.AddRange(AiAnalysisRules.Check(aiResponse, hasForeignTrackers));
                    }
                    catch (Exception ex)
                    {
                        all.Add(CheckResult.Create(Severity.Info, "Анализ Политики (ИИ)", "Не удалось выполнить анализ Политики с помощью ИИ", "", "", ex.Message));
                    }
                }
                else if (privacyDocument != null && !string.IsNullOrEmpty(privacyDocument.Url))
                {
                    all.Add(CheckResult.Create(Severity.Info, "Анализ Политики (ИИ)", "Текст Политики не удалось получить или он слишком короткий для анализа", "", "", "Возможно, документ в формате PDF или загружается скриптом: проверьте вручную"));
                }
                _lastResults = all;

                var critical = all.Where(r => r.Severity == Severity.Critical).GroupBy(c => c.Title).Select(g => g.First()).Select(r => new ReportLine { Title = r.Title }).ToList();
                var warnings = all.Where(r => r.Severity == Severity.Warning || r.Severity == Severity.Info).GroupBy(c => c.Title).Select(g => g.First()).Select(r => new ReportLine { Title = r.Title }).ToList();
                var passed = all.Where(r => r.Severity == Severity.Passed).GroupBy(c => c.Title).Select(g => g.First()).Select(r => new ReportLine { Title = r.Title }).ToList();

                CriticalList.ItemsSource = critical;
                WarningList.ItemsSource = warnings;
                PassedList.ItemsSource = passed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке: {ex.Message}", "Ошибка");
            }
            finally
            {
                if (scanner != null) await scanner.CloseAsync();
                CheckButton.IsEnabled = true;
                CheckButton.Content = "Проверить";

                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
        private async void GenerateReportButton_Click(object sender, RoutedEventArgs e) {
            if (_lastResults == null || !_lastResults.Any())
            {
                MessageBox.Show("Сначала запустите проверку сайта.", "Нет данных");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF файл (*.pdf)|*.pdf",
                FileName = $"PdnAudit_Report_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    GenerateReportButton.IsEnabled = false;
                    GenerateReportButton.Content = "Генерация...";

                    var reporter = new ReportGenerator();
                    await reporter.GeneratePdfReportAsync(_lastResults, UrlTextBox.Text, saveFileDialog.FileName);

                    MessageBox.Show($"Отчет успешно сохранен:\n{saveFileDialog.FileName}", "Успех");

                    // Опционально: открыть PDF сразу
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(saveFileDialog.FileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка генерации отчета: {ex.Message}", "Ошибка");
                }
                finally
                {
                    GenerateReportButton.IsEnabled = true;
                    GenerateReportButton.Content = "Сформировать отчет";
                }
            }
        }
    }
}