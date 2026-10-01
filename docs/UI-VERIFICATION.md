# Интерфейс SimDeck 0.9.2

Согласованные композиции реализованы в настоящих Android и браузерном клиентах. У F1 24 и F1 25 общий красный Race Control. Остальные игры имеют отдельные приборы, баннеры и быстрые действия, а не только другую палитру.

## Проверка

- 279 проверок .NET; 49 Android unit tests. Сборки Companion и APK проходят.
- Автономный Companion запускается и отдаёт HTTPS с проверкой сертификата; нормальное закрытие и закрытие после ошибки занятого порта проходят. Эти проверки добавлены в GitHub Actions.
- Android: все восемь профилей просмотрены на подключённом планшете Samsung SM-X115 в горизонтальной ориентации и при эмуляции узкого экрана телефона. APK 0.9.2 установлен с сохранением данных приложения. Нативный интерфейс проверен снимками и деревом UI, зажигание BeamNG — отдельным нажатием и последующим удержанием; события записаны тестовым хостом.
- Браузер: Chromium и WebKit, размеры 320×640, 390×844, 844×390, 800×1340, 1340×800. Проверены все профили, пять экранов F1, добавленная пользовательская страница, доступность назначенных действий, нажатие, удержание/отпускание, блокировка при выключенном вводе, отсутствие данных AMS2/SnowRunner и горизонтального переполнения. Ошибок JavaScript нет.

На снимках ниже — **работающие приложения с тестовыми показаниями**, а не сгенерированные изображения. Android снят с устройства; браузер снят при проверке WebKit. Системные панели Android обрезаны. Настройки подключения и личные IP в галерею не включены. [Команды воспроизведения проверки](../tools/qa/README.md).

Эти проверки подтверждают экран и протокол команд. Они не заменяют проверку всех игровых биндов в установленной игре; WebKit на Windows не заменяет физический iPhone. AMS2 и SnowRunner пока не имеют живой телеметрии, у ETS2 доступны маршрутные числа, а не карта дорог.

## Планшет: Android и браузер

| Профиль | Android | WebKit |
|---|---|---|
| F1 24 | ![F1 24 Android](images/profiles/f1-24-tablet.png) | ![F1 24 WebKit](images/profiles/f1-24-browser-tablet.png) |
| F1 25 | ![F1 25 Android](images/profiles/f1-25-tablet.png) | ![F1 25 WebKit](images/profiles/f1-25-browser-tablet.png) |
| BeamNG | ![BeamNG Android](images/profiles/beamng-default-tablet.png) | ![BeamNG WebKit](images/profiles/beamng-default-browser-tablet.png) |
| ACC | ![ACC Android](images/profiles/acc-tablet.png) | ![ACC WebKit](images/profiles/acc-browser-tablet.png) |
| AMS2 | ![AMS2 Android](images/profiles/ams2-tablet.png) | ![AMS2 WebKit](images/profiles/ams2-browser-tablet.png) |
| ETS2 | ![ETS2 Android](images/profiles/ets2-tablet.png) | ![ETS2 WebKit](images/profiles/ets2-browser-tablet.png) |
| SnowRunner | ![SnowRunner Android](images/profiles/snowrunner-tablet.png) | ![SnowRunner WebKit](images/profiles/snowrunner-browser-tablet.png) |
| FS25 | ![FS25 Android](images/profiles/fs25-tablet.png) | ![FS25 WebKit](images/profiles/fs25-browser-tablet.png) |

## Телефон: Android и браузер

| Профиль | Android | WebKit |
|---|---|---|
| F1 24 | ![F1 24 Android phone](images/profiles/f1-24-phone.png) | ![F1 24 WebKit phone](images/profiles/f1-24-browser-phone.png) |
| F1 25 | ![F1 25 Android phone](images/profiles/f1-25-phone.png) | ![F1 25 WebKit phone](images/profiles/f1-25-browser-phone.png) |
| BeamNG | ![BeamNG Android phone](images/profiles/beamng-default-phone.png) | ![BeamNG WebKit phone](images/profiles/beamng-default-browser-phone.png) |
| ACC | ![ACC Android phone](images/profiles/acc-phone.png) | ![ACC WebKit phone](images/profiles/acc-browser-phone.png) |
| AMS2 | ![AMS2 Android phone](images/profiles/ams2-phone.png) | ![AMS2 WebKit phone](images/profiles/ams2-browser-phone.png) |
| ETS2 | ![ETS2 Android phone](images/profiles/ets2-phone.png) | ![ETS2 WebKit phone](images/profiles/ets2-browser-phone.png) |
| SnowRunner | ![SnowRunner Android phone](images/profiles/snowrunner-phone.png) | ![SnowRunner WebKit phone](images/profiles/snowrunner-browser-phone.png) |
| FS25 | ![FS25 Android phone](images/profiles/fs25-phone.png) | ![FS25 WebKit phone](images/profiles/fs25-browser-phone.png) |
