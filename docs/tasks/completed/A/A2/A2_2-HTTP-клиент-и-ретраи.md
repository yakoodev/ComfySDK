# Task A2_2 — HTTP клиент и ретраи

## Цель
Сделать нормальный HTTP слой: таймауты, ретраи, ошибки, чтобы не было “иногда работает, иногда да пошло оно”.

## Объём работ
- Настроить `HttpClient` через `IHttpClientFactory`.
- Retry policy: сетевые ошибки, 429, 5xx, с jittered backoff.
- Раздельные таймауты для upload/download.
- Unified error handling → `ComfyException`.

## Критерии приёмки
- При 500/429 клиент делает ретраи по политике.
- При 4xx (кроме 429) ретраев нет.
- Исключения содержат route/status/body snippet.
- Логи содержат requestId/promptId если есть.

## Как проверить
- Смоделировать 429 в тестах (fake handler) и проверить число попыток.
- Смоделировать 400 и убедиться, что ретраев нет.

## Выполнено
- Добавлен HTTP transport `ComfyHttpClient` с retry policy (сеть/429/5xx + jittered backoff).
- Добавлены раздельные таймауты в `ComfyClientOptions`: `DefaultTimeout`, `UploadTimeout`, `DownloadTimeout`.
- Добавлены retry-настройки `ComfyRetryOptions`.
- Реализован unified error handling через `ComfyException` (`route/status/requestId/promptId/bodySnippet`).
- DI обновлён: `ComfyHttpClient` регистрируется через `IHttpClientFactory` (`AddHttpClient`).
- В тестовом раннере добавлены проверки:
  - `429` → есть ретраи и успех после повторов.
  - `400` → без ретраев, выбрасывается `ComfyException` с полями.
