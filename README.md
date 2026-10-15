🇷🇺 **Русский** • [🇬🇧 English](docs/EN/README.md)

<div align="center">

# Antigravity DNS

### Автоматический DNS/DoH benchmark и bridge для Antigravity, Gemini и Google-сервисов на Windows.
<br>

[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=flat-square&logo=windows11&logoColor=white)](#)
[![License](https://img.shields.io/github/license/narechiyaprincess/Antigravity-DNS?style=flat-square)](LICENSE)
[![Release](https://img.shields.io/github/v/release/narechiyaprincess/Antigravity-DNS?style=flat-square&label=release)](https://github.com/narechiyaprincess/Antigravity-DNS/releases)
[![Downloads](https://img.shields.io/github/downloads/narechiyaprincess/Antigravity-DNS/total?style=flat-square)](https://github.com/narechiyaprincess/Antigravity-DNS/releases)
[![Open Source](https://img.shields.io/badge/open--source-yes-success?style=flat-square)](LICENSE)

</div>

---

**Antigravity DNS** — небольшая Windows-утилита для автоматического поиска и подключения рабочего DNS/DoH-провайдера для:

- **Antigravity**
- **Gemini Desktop**
- **Google-сервисов в браузере**
- других совместимых сценариев, где доступ зависит от DNS/DoH

Вместо ручного перебора серверов приложение само запускает серию проверок, сравнивает доступные варианты и определяет наиболее подходящий рабочий провайдер.

Основной workflow:

```text
запустил
   ↓
замерил провайдеров
   ↓
получил лучший рабочий вариант
   ↓
включил bridge
   ↓
забыл
