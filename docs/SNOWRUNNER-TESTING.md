# Проверка SnowRunner 0.9.28

## Повторяемые проверки без игры

- `dotnet run --project companion/SimDeck.Tests -c Release`: границы данных, независимые байтовые состояния с ненулевыми соседними байтами, импорт NumPad, миграция пользовательских клавиш, каталог с Windows ZIP-путями и отказ от неоднозначных моделей.
- В `android`: `gradle :app:testTabletDebugUnitTest :app:testPhoneDebugUnitTest` — протокол SnowRunner и вкладки телефона, затем `:app:assembleTabletDebug :app:assemblePhoneDebug`.
- Экспортируйте каталог: `dotnet run --project companion/SimDeck.Tests -c Release -- --export-profiles artifacts/profiles.json`.
- Запустите локальный HTTP-сервер папки `companion/SimDeck.App/Browser`. Выполните `node tools/qa/check-snowrunner.cjs artifacts/profiles.json tools/qa/fixtures/snowrunner.json http://127.0.0.1:18887/ artifacts/snow-screens`. Нужен Playwright; `SIMDECK_BROWSER=webkit` проверяет Safari. Файл фикстуры — пример для проверки интерфейса, он не подключается к рабочей телеметрии.

Проверяются три размера экрана, схемы 4/6/8/10 колёс, неизвестные состояния, независимые AWD/блокировка, показания при задержке, доступность кнопок и всех настроенных вкладок.

## Проверка на живой игре

Проверено на `1.886173.SNOW_DLC_18`: Western Star 6900 TwinSteer (8 колёс) и Voron Grad (6 колёс), смена машины, топливо и прочность узлов. Voron Grad: AWD включается/выключается с планшета; блокировка на L переключается в обе стороны. Поля состояния сверены с HUD. Кнопки автоматической и пониженной передачи также проверены.

Companion и планшет должны быть одной версии, игра активна, грузовик стоит на ручнике. Не нажимайте газ при проверке переключателей. `--snow-read <путь.json>` сохраняет один кадр и диагностирует адаптер; необработанные дампы памяти не публикуются. Для неизвестного EXE ожидается отказ адаптера с доступными отдельными клавишами. RPM и передача показываются прочерками до подключения их источника.
