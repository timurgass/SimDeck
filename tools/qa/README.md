# Проверка экранов Android и Safari

## SimDeck Phone (0.9.15)

В `android` выполните `./gradlew testPhoneDebugUnitTest testTabletDebugUnitTest assemblePhoneDebug assembleTabletDebug`. Установите `app/build/outputs/apk/phone/debug/app-phone-debug.apk` на один разрешённый для USB-отладки телефон.

Из корня: `python tools/qa/check-phone-device.py <путь-к-adb> artifacts/phone-0915`. Скрипт получает 36 вертикальных и 9 горизонтальных снимков настоящего Compose-интерфейса, проверяет их целостность/ориентацию, открывает «Ещё» касанием и возвращается кнопкой Android Back в каждом профиле. Настройки поворота восстанавливаются в `finally`. Снимки содержат только область приложения. `--resume` сохраняет уже полученные вертикальные снимки и повторяет горизонтальные/навигацию.

Просмотр включается только явными debug-extras `preview_profile`, `preview_tab`, `preview_capture`. Он использует заводские профили из `android/app/src/debug/assets/phone-preview-profiles.json`, не устанавливает сетевое соединение и не отправляет игровой ввод. Результат `result.json` появляется после успешного завершения всех проверок. Перед новым запуском не считайте оставшийся от прежнего запуска результат доказательством успеха.

При изменении заводских действий обновите фикстуру через `dotnet run --project companion/SimDeck.Tests -c Release -- --export-profiles android/app/src/debug/assets/phone-preview-profiles.json`. Тест `PhoneNavigationTest` сверяет все быстрые кнопки с этими реальными ID и сохраняет пользовательские страницы/жесты.

Для проверки отправки команд используйте записывающий хост ниже. Просмотр и снимки сами по себе не подтверждают бинды установленной игры. [Телефонные экраны и ограничения](../../docs/PHONE-0915.md).

## Автоматический выбор техники (0.9.3)

`python -m pip install lupa==2.8`, затем `python tools/qa/check-vehicle-mods.py` из корня: выполняются исходники Lua-модов, проверяются FS25-цепочка/смена машины/выход и реальный FFI-пакет BeamNG SMD3. Это тестовые игровые API, не запуск игры.

`check-dashboards.cjs` дополнительно проверяет смену шестиколёсного тягача на неизвестную машину, удаление предыдущей геометрии, прозрачный прицеп ETS2 и выход из техники на всех пяти размерах. C# и Android тесты проверяют границы массивов, иерархию и неизвестные значения.

Из корня репозитория экспортируйте **заводские**, а не личные профили:

```powershell
dotnet run --project companion/SimDeck.Tests -c Release -- --export-profiles artifacts/profiles.json
python tools/qa/serve-dashboard.py --port 8765
```

В другом терминале установите зависимости и выполните проверку:

```powershell
cd tools/qa
npm install
npx playwright install chromium webkit
node check-dashboards.cjs ../../artifacts/profiles.json http://127.0.0.1:8765 ../../artifacts
$env:SIMDECK_BROWSER = 'webkit'
node check-dashboards.cjs ../../artifacts/profiles.json http://127.0.0.1:8765 ../../artifacts
```

`SIMDECK_BROWSER_EXECUTABLE` позволяет использовать установленный Chromium-браузер. Для WebKit оставьте эту переменную пустой. Скрипт проверяет все девять профилей на размерах 320×640, 390×844, 844×390, 800×1340 и 1340×800: переполнение, навигацию F1, доступность пользовательских действий, отправку нажатия, удержание/отпускание, блокировку ввода и отсутствие выдуманной телеметрии AMS2/SnowRunner. Снимки создаются для телефона и планшета. WebSocket и игровые показания заменены фикстурами; проверка не отправляет команды в игру и не подтверждает бинды установленной игры. WebKit на Windows также не заменяет проверку на физическом iPhone.

Для визуальной проверки **настоящего Compose-интерфейса** есть отдельный записывающий хост:

```powershell
dotnet run --project companion/SimDeck.Tests -c Release -- --dashboard-server artifacts/dashboard-state
```

Он работает до создания файла `artifacts/dashboard-state/stop`, максимум 30 минут. Записывайте ID профиля в `profile.txt` внутри этой папки, чтобы переключить экран. Сопряжение организуйте через изолированное состояние; не меняйте рабочие настройки Companion. Телеметрия тестовая, клавиатурный ввод только записывается в `input-events.json`. Не публикуйте настройки, сертификаты или сведения сопряжения из этой папки.

Дополнительно проверьте на устройстве короткое нажатие и последующее удержание зажигания BeamNG, ориентации и возврат из экрана подключения. После QA восстановите размер, ориентацию и подключение устройства. Публичные снимки обрежьте по границам приложения, удалите IP/коды сопряжения и обозначьте тестовые показания.

FS25 0.9.6: run `python tools/qa/generate-fs25-registry.py --check` before building. This validates generated C#/Kotlin/JS/Lua parity and PNG source bounds. `check-vehicle-mods.py` covers 103 store categories and the important equipment capability refinements. With the local dashboard server running on port 18978, `node tools/qa/check-fs25-catalog.cjs` exercises all 92 illustrated classes in the actual renderer. It accepts a server URL as the first argument and uses the same Chromium/WebKit environment options as the main dashboard check.

ATS и ETS2 используют общий набор SCS-кнопок. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/qa/check-scs-installers.ps1` проверяет оба пресета на синтетических файлах: предварительный просмотр, 29 назначений, сохранность руля/пользовательских команд, резервную копию и повторную установку. Повторный запуск используйте с новой `-Directory artifacts/scs-test-2`.
