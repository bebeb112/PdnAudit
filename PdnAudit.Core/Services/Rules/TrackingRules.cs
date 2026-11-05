using PdnAudit.Core.Models;
using PdnAudit.Core.Models.Tracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services.Rules {
    public class TrackingRules {
        private const string Cat = "Cookies и аналитика";

        private static bool IsTracker(string category) =>
            category == TrackerCategory.Analytics || category == TrackerCategory.Ads;

        public static List<CheckResult> Check(TrackingResult t) {
            var results = new List<CheckResult>();
            var before = t.Before;
            var after = t.After;

            // Трекеры по сетевым запросам и по cookies
            var trackersBefore = before.Requests.Where(r => IsTracker(r.Category)).Select(r => r.ServiceName)
                .Concat(before.Cookies.Where(c => IsTracker(c.Category)).Select(c => c.ServiceName))
                .Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

            var trackersAfter = after.Requests.Where(r => IsTracker(r.Category)).Select(r => r.ServiceName)
                .Concat(after.Cookies.Where(c => IsTracker(c.Category)).Select(c => c.ServiceName))
                .Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

            bool anyTrackers = trackersBefore.Count > 0 || trackersAfter.Count > 0;
            var banner = before.Banner;

            // ---------- 1. Аналитика и реклама до согласия ----------
            if (trackersBefore.Count > 0)
            {
                string list = string.Join(", ", trackersBefore);
                if (!banner.Found)
                    results.Add(CheckResult.Create(Severity.Critical, Cat,
                        "Используются сервисы аналитики или рекламы, уведомления о cookies не обнаружено",
                        "ст. 6, 9, 18.1 152-ФЗ",
                        "Разместить уведомление об использовании cookies и получать согласие до подключения аналитики и рекламы",
                        $"Обнаружено до каких-либо действий пользователя: {list}. Баннер не найден автоматически"));
                else
                    results.Add(CheckResult.Create(Severity.Critical, Cat,
                        "Сервисы аналитики или рекламы загружаются до получения согласия",
                        "ст. 6, 9 152-ФЗ",
                        "Подключать аналитику и рекламу только после того, как посетитель принял cookies",
                        $"Обнаружено до нажатия «Принять»: {list}"));
            }
            else if (trackersAfter.Count > 0)
            {
                results.Add(CheckResult.Create(Severity.Passed, Cat,
                    "Аналитика и реклама подключаются только после согласия", "ст. 9 152-ФЗ", "",
                    $"Появились после нажатия «Принять»: {string.Join(", ", trackersAfter)}"));
            }
            else
            {
                results.Add(CheckResult.Create(Severity.Passed, Cat,
                    "Сервисы аналитики и рекламы не обнаружены", "ст. 6, 9 152-ФЗ", "",
                    "Проверены сетевые запросы и cookies главной страницы"));
            }

            // ---------- 2. Баннер cookies ----------
            if (banner.Found)
            {
                if (!banner.HasReject && !banner.HasSettings)
                    results.Add(CheckResult.Create(Severity.Warning, Cat,
                        "В уведомлении о cookies нет возможности отказаться или настроить",
                        "ст. 9 152-ФЗ",
                        "Добавить кнопки «Отклонить» или «Настроить», чтобы согласие было добровольным",
                        $"Текст баннера: {Short(banner.Text)}"));

                if (!banner.HasPolicyLink)
                    results.Add(CheckResult.Create(Severity.Warning, Cat,
                        "В уведомлении о cookies нет ссылки на политику",
                        "ст. 18.1 152-ФЗ",
                        "Добавить в баннер ссылку на Политику обработки персональных данных или политику cookies"));

                if ((banner.HasReject || banner.HasSettings) && banner.HasPolicyLink)
                    results.Add(CheckResult.Create(Severity.Passed, Cat,
                        "Уведомление о cookies содержит выбор и ссылку на политику", "ст. 9, 18.1 152-ФЗ", ""));
            }
            else if (anyTrackers)
            {
                // Уже отражено в критическом пункте выше, отдельное замечание не дублируем
            }
            else
            {
                results.Add(CheckResult.Create(Severity.Passed, Cat,
                    "Уведомление о cookies не требуется: трекеры не обнаружены", "", "",
                    "Баннер не найден автоматически"));
            }

            // ---------- 3. Признаки трансграничной передачи ----------
            var allReq = before.Requests.Concat(after.Requests)
                .Where(r => r.IsForeign)
                .GroupBy(r => r.Host)
                .Select(g => g.First())
                .ToList();

            var foreignTrack = allReq.Where(r => IsTracker(r.Category)).ToList();
            var foreignWidgets = allReq.Where(r => r.Category == TrackerCategory.Widget || r.Category == TrackerCategory.Video).ToList();
            var foreignRes = allReq.Where(r => r.Category == TrackerCategory.Resource || r.Category == TrackerCategory.Other).ToList();

            if (foreignTrack.Count > 0)
                results.Add(CheckResult.Create(Severity.Critical, Cat,
                    "Обнаружены признаки трансграничной передачи данных (зарубежные сервисы аналитики или рекламы)",
                    "ст. 12 152-ФЗ",
                    "Убедиться, что передача допустима, уведомить Роскомнадзор о трансграничной передаче, указать это в Политике или заменить сервисы российскими аналогами",
                    string.Join("; ", foreignTrack.Select(r => $"{r.ServiceName} ({r.Host})"))));

            if (foreignWidgets.Count > 0)
                results.Add(CheckResult.Create(Severity.Warning, Cat,
                    "Подключены зарубежные чаты, виджеты или видео, возможна передача данных за рубеж",
                    "ст. 12 152-ФЗ",
                    "Проверить, какие данные получают эти сервисы, и отразить их в Политике",
                    string.Join("; ", foreignWidgets.Select(r => $"{r.ServiceName} ({r.Host})"))));

            if (foreignRes.Count > 0)
                results.Add(CheckResult.Create(Severity.Info, Cat,
                    "Загружаются ресурсы с зарубежных серверов (шрифты, карты, CDN)",
                    "ст. 12 152-ФЗ",
                    "По возможности размещать шрифты и библиотеки на собственном сервере: при загрузке с внешних серверов им передаётся IP-адрес посетителя",
                    string.Join("; ", foreignRes.Select(r => r.Host).Take(10))));

            if (foreignTrack.Count == 0 && foreignWidgets.Count == 0 && foreignRes.Count == 0)
                results.Add(CheckResult.Create(Severity.Passed, Cat,
                    "Признаков трансграничной передачи не обнаружено", "ст. 12 152-ФЗ", "",
                    "Зарубежные сторонние сервисы на проверенных страницах не найдены"));

            // ---------- 4. Ошибки сбора (честность отчёта) ----------
            if (before.Errors.Count > 0)
                results.Add(CheckResult.Create(Severity.Info, Cat,
                    "При проверке cookies возникли ошибки, результат может быть неполным", "", "",
                    string.Join("; ", before.Errors)));

            return results;
        }

        private static string Short(string s) => s.Length <= 200 ? s : s[..200] + "...";
    }
}
