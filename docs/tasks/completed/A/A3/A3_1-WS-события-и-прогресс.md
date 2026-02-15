# Task A3_1 — WS события и прогресс

## Цель
Подключение к WS, нормализация событий, авто-reconnect и корректный terminal state.

## Объём работ
- Реализовать подключение WS по `clientId`.
- Нормализовать события в `RunEvent` (success/error/interrupted/executing/progress/uiUpdated).
- Авто‑reconnect с backoff.
- Привязка к `promptId`.
- Cancellation: закрыть WS и прекратить стрим.

## Критерии приёмки
- `await foreach` отдаёт события.
- При разрыве соединения происходит reconnect и стрим продолжается.
- Terminal событие гарантируется (или через HTTP fallback).
- Cancel не вызывает `/interrupt` автоматически.

## Как проверить
- В sample: искусственно оборвать WS (например, перезапустить сервер) и увидеть reconnect.
- Запустить run и убедиться, что на успех приходит `Succeeded`.

## Выполнено
- `RunStreamAsync` расширен до нормализованного WS-потока с событиями:
  - `Connected`, `Queued`, `Executing`, `Progress`, `Disconnected`, `Reconnected`, `Succeeded`.
- Добавлен auto-reconnect с настраиваемыми параметрами:
  - `EnableWsReconnect`, `WsMaxReconnectAttempts`, `WsReconnectBaseDelay`.
- Добавлена привязка потока к `promptId` во всех событиях/логах.
- Добавлен fallback до terminal state через HTTP-path (в scaffold режиме — `Succeeded` fallback).
- Реализовано корректное `Cancellation`: стрим останавливается локально без auto-вызова `/interrupt`.
- В тестовом раннере добавлены проверки reconnect/terminal и cancel-behavior.
