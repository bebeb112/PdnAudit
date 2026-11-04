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
using PdnAudit.Core.Services;
using PdnAudit.Core.Models;

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
            await Task.Delay(5000);
            try
            {
                SiteScanner scanner = new SiteScanner();

                await scanner.StartBrowserAsync();

                var (privacyDocument, consentDocument) = await scanner.GetPdnDocuments();

                ConsentDocumentTextBlock.Text += consentDocument.Url;
                PrivacyDocumentTextBlock.Text += privacyDocument.Url;
                LinksAmountTextBlock.Text = scanner.linksAmount.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при работе сканера: {ex.Message}", "Ошибка");
            }
            /* try
             {
                 SiteScanner scanner = new SiteScanner();

                 ScanResult scanResult = await scanner.ScanSiteAsync();

                 LinksAmountTextBlock.Text += scanResult.Links.Count;
                 FormsAmountTextBlock.Text += scanResult.Forms.Count;
             }
             catch(Exception ex)
             {
                 MessageBox.Show($"Ошибка при работе сканера: {ex.Message}", "Ошибка");
             } */

        }
    }
}