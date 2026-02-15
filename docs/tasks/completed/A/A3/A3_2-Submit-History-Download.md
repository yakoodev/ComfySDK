# Task A3_2 — Submit/History/Download

## Цель
Реализовать submit prompt, получение history (v1/v2) и скачивание outputs с redirect.

## Объём работ
- `SubmitAsync(workflowJson)` → promptId.
- `GetHistoryAsync(promptId)` поддерживает v1/v2 через RouteMap.
- `DownloadAsync(viewParams)` следует редиректам.
- Превратить history в `OutputArtifact` список.

## Критерии приёмки
- Работает на Server и Cloud (при корректном RouteMap).
- Download следует 302/307.
- Из history строится понятный список outputs (type/name/url/etc).

## Как проверить
- В sample: после run скачать первый image output.
- В тесте: mock 302 redirect на download и убедиться, что скачалось.

## Выполнено
- Добавлены API-методы в `ComfyClient`:
  - `SubmitAsync(workflowJson)` → `promptId`
  - `GetHistoryAsync(promptId)` (через `HistoryV2` route) → `IReadOnlyList<OutputArtifact>`
  - `DownloadAsync(ViewParams)` с follow redirects.
- В `ComfyHttpClient` добавлен `GetWithRedirectsAsync(...)` с поддержкой 302/307/308 (и других 3xx).
- Реализовано преобразование history JSON в `OutputArtifact` (`name/type/url`).
- `RunAsync` переведён на цепочку `SubmitAsync` + `GetHistoryAsync`.
- В sample добавлена попытка скачать первый image output после run (с безопасным fallback для локального запуска без endpoint).
- В тестовом раннере добавлены проверки:
  - redirect download (302 -> 200);
  - submit + history mapping в outputs.
