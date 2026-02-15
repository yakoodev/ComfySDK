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
