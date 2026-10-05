# Источник форматов игрового GPS

Форматы Prism (`gps_manager`, `simple_route_source`, `route_task`, `physical_route_item`, `node_item`) исследованы по [ETS2LA/ets2la_plugin](https://gitlab.com/ETS2LA/ets2la_plugin), исходный commit `c925bd965a96c58b73eaea9ca033fe0f0dc623a7`. Сигнатура `base_ctrl` для 1.61 сверена с опубликованным плагином в `ETS2LA/ETS2LA/Assets/SDKs/1.61/Windows/ets2la_plugin.dll`.

SimDeck читает память выбранной игры с `PROCESS_VM_READ`, не загружает библиотеку ETS2LA, не вызывает игровые функции и не пишет в память игры. GPL-код клиента ETS2LA не включён.

Исходный плагин распространяется по MIT:

Copyright (c) 2024 Dario Wouters

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
