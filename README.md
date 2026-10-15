🇷🇺 Русский • [🇬🇧 English](docs/EN/README.md)

# Antigravity DNS

**DNS-мост для Antigravity, Gemini Desktop и сервисов Google в Windows.**

[![Windows x64](https://img.shields.io/badge/Windows-x64-0078D4?style=flat-square)](#установка)
[![C# · WinForms](https://img.shields.io/badge/C%23-WinForms-512BD4?style=flat-square)](docs/BUILDING.md)
[![MIT](https://img.shields.io/github/license/narechiyaprincess/Antigravity-DNS?style=flat-square)](LICENSE)
[![Latest release](https://img.shields.io/github/v/release/narechiyaprincess/Antigravity-DNS?style=flat-square)](https://github.com/narechiyaprincess/Antigravity-DNS/releases/latest)

Проверяет DNS/DoH-провайдеров, помогает выбрать рабочий и настраивает Windows для доступа к выбранным доменам через DNS/SNI-прокси провайдера. Один portable EXE, русский и английский интерфейс.

[Скачать последнюю версию](https://github.com/narechiyaprincess/Antigravity-DNS/releases/latest) · [Issues](https://github.com/narechiyaprincess/Antigravity-DNS/issues) · [Telegram](https://t.me/sexelysium) · [Сборка](docs/BUILDING.md)

<p>
  <img src="assets/screenshots/bridge.png" width="320" alt="Мост: профиль провайдера и область действия">
  <img src="assets/screenshots/benchmark.png" width="320" alt="Замер: доступность, медиана, P95 и TLS">
</p>

*Результаты замера зависят от сети.*

## Возможности

- Автоматическая проверка встроенных DNS/DoH-профилей: доступность, latency, медиана, P95 и TLS; ранжирование и выбор лучшего.
- Области **Antigravity**, **+ Gemini Desktop**, **+ Браузер** для доменов Google; свой DoH-профиль.
- Проверки перед применением, журнал собственных правил и восстановление с сохранением чужих или общих записей.
- Трей, автозапуск, уведомления, логи, история замеров и операций, отчёт для поддержки.

## Как это работает

1. **Измерить** — приложение проверит провайдеров в вашей сети.
2. **Выбрать** — сравните результаты или нажмите «Выбрать лучший».
3. **Подключить мост** — подтвердите провайдера и UAC, затем откройте нужное приложение.

Antigravity DNS не является VPN и не туннелирует весь трафик. Он применяет DNS-правила для выбранных доменов; DNS адаптеров, `hosts` и системный прокси не переписываются.

## Установка

Нужны **Windows 11 x64**, системный DoH и Windows PowerShell 5.1. Для подключения и восстановления требуются права администратора. .NET runtime включён в EXE; установщик не нужен. Совместимость с Windows 10 не подтверждена.

Скачайте EXE из [последнего Release](https://github.com/narechiyaprincess/Antigravity-DNS/releases/latest), сверьте SHA-256 с данными выпуска и запустите из постоянной папки.

## Использование

На вкладке **«Замер»** нажмите **«Измерить»**, затем **«Выбрать лучший»** или выберите профиль вручную. На вкладке **«Мост»** задайте область и нажмите **«Подключить мост»**. Выбор профиля сам по себе не подключает мост.

**«+ Gemini Desktop»** добавляет домены Gemini, **«+ Браузер»** — домены Google. Свой DoH и автозапуск доступны в **«Настройках»**. Для отключения используйте **«Отключить мост» / «Восстановить»**; закрытие окна и выход из трея не удаляют правила.

## Важно

- Провайдер обрабатывает DNS-запросы и предоставляет SNI-прокси. Проверка TLS не заменяет доверие к нему; успешный замер не гарантирует доступ аккаунта или ответ модели.
- Secure DNS браузера и VPN/TUN могут обходить системный резолвер. Настройки браузера приложение не меняет.
- [Домены, безопасность, локальные данные и диагностика](docs/TECHNICAL.md). Перед отправкой отчёта поддержки проверьте предпросмотр.

## Для разработчиков

C# WinForms и PowerShell; каталог провайдеров и движок встроены в EXE. Исходники, offline-тесты и инструменты находятся в `src/`. Команды сборки, проверки и ограничения упаковки — в [BUILDING.md](docs/BUILDING.md).

## Лицензия и ссылки

[MIT](LICENSE) · [Исходники](https://github.com/narechiyaprincess/Antigravity-DNS) · [Open Antigravity Patcher](https://sourceforge.net/projects/open-antigravity-patcher/)

Патчер — отдельный проект, доступный по ссылке из приложения. Antigravity DNS не является официальным проектом Google.
