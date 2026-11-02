using Microsoft.Playwright;
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

            Loaded += MainWindow_Loaded;
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            try
            {
                await StartBrowserAsync();

                List<string> foundLinks = await GetLinksAsync();
                LinksAmountTextBlock.Text += foundLinks.Count.ToString();

                List<string> foundFormsIds = await GetFormsAsync();
                FormsAmountTextBlock.Text += foundFormsIds.Count.ToString();
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации: {ex.Message}", "Ошибка");
            }

        }
        private async Task<List<string>> GetLinksAsync() {
            if (page == null) return new List<string>();

            var linkLocators = await page.GetByRole(AriaRole.Link).AllAsync();
            var links = new List<string>();

            foreach(var locator in linkLocators)
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
            try
            {
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

                string title = await page.TitleAsync();

            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
            
        }
    }
}