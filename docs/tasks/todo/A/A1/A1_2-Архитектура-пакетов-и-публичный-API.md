# Task A1_2 — Архитектура пакетов и публичный API

## Цель
Спроектировать public API так, чтобы оно было расширяемым и не завязано на конкретные роуты/форматы Cloud/Server.

## Объём работ
- Определить классы: `ComfyClient`, `ComfyClientOptions`, `RouteMap`, `IAuthProvider`.
- Определить модели: `RunHandle`, `RunEvent`, `RunResult`, `OutputArtifact`.
- Определить интерфейсы: `IFileResolver`, `IFileUploader`, `IDownloader`.
- Встроить `ILogger` поддержку.
- Убедиться, что параллельные запуски возможны (нет shared state).

## Критерии приёмки
- Публичные типы имеют XML-docs.
- `ComfyClient` можно создать через DI и напрямую.
- Есть `RouteMap`/`ApiPrefix` и очевидный способ подменить.
- `RunStreamAsync` выдаёт `IAsyncEnumerable<RunEvent>`.
- `RunAsync` возвращает `RunResult` с outputs.

## Как проверить
- Скомпилировать solution.
- В sample коде создать 2 параллельных run и убедиться, что оба отрабатывают.
- Проверить, что без настройки маршрутов работают дефолты для Server.
