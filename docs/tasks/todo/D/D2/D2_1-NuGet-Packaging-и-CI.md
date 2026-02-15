# Task D2_1 — NuGet packaging и CI

## Цель
Сделать упаковку, CI и smoke тесты (без бюрократии, но чтобы не развалилось).

## Объём работ
- Настроить `dotnet pack` для runtime пакета.
- Опционально: publish generator как dotnet tool.
- CI: build + unit tests.
- Smoke test: минимальный workflow материализуется (без реального comfy).

## Критерии приёмки
- NuGet пакеты собираются.
- CI зелёный.
- Версии и metadata нормальные (license, repo, readme).

## Как проверить
- Запустить workflow CI локально.
- Убедиться, что `dotnet pack` даёт .nupkg.
