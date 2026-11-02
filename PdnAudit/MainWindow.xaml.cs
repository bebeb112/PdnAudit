using Microsoft.Playwright;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PdnAudit
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow() {
            InitializeComponent();


            // Создаем кнопку на лету
            var button = new System.Windows.Controls.Button
            {
                Content = "Проверить Playwright",
                Width = 200,
                Height = 50,
                Margin = new Thickness(50)
            };

            // Привязываем к ней событие клика
            button.Click += async (s, e) =>
            {
                button.IsEnabled = false;
                button.Content = "Загрузка...";

                try
                {
                    // 1. Инициализируем Playwright
                    using var playwright = await Playwright.CreateAsync();

                    // 2. Запускаем браузер (headless: false — чтобы вы увидели окно браузера)
                    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = false // Поставьте true, если не хотите видеть окно браузера
                    });

                    // 3. Открываем новую страницу
                    var page = await browser.NewPageAsync();

                    // 4. Переходим на сайт (например, на Яндекс)
                    await page.GotoAsync("https://ya.ru");

                    // 5. Получаем заголовок страницы (тег <title>)
                    string title = await page.TitleAsync();

                    // 6. Показываем результат
                    MessageBox.Show($"Успех! Заголовок сайта: {title}", "Playwright работает!");
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}\n\nУбедитесь, что вы запустили playwright.ps1 install", "Ошибка");
                }
                finally
                {
                    button.IsEnabled = true;
                    button.Content = "Проверить Playwright";
                }
            };

            // Добавляем кнопку на форму
            this.Content = button;
        }
    }
}