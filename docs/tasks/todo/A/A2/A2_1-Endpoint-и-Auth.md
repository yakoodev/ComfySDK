# Task A2_1 — Endpoint и Auth

## Цель
Сделать поддержку распространённых auth схем и возможность конфигурировать маршруты под Server/Cloud.

## Объём работ
- Реализовать `IAuthProvider`: None, Bearer, ApiKeyHeader, Cookie (минимально).
- Реализовать `RouteMap`:
  - SubmitPrompt
  - HistoryV1, HistoryV2
  - View
  - UploadImage, UploadMask (если есть)
  - Queue/Status (опционально)
  - WsPath
- Добавить `ApiPrefix` как sugar (например `/api`).
- Добавить masking секретов в логах (headers/cookies/query).

## Критерии приёмки
- Можно переключить Server/Cloud конфигом без переписывания клиента.
- Секреты не попадают в логи.
- Поддержаны Bearer и cookie минимум.

## Как проверить
- Написать unit-тест на masking (строка лога не содержит токен).
- В samples: показать Cloud config с `ApiPrefix` и Bearer.
