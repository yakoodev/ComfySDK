# Task C2_1 — Materializer: Params → Workflow

## Цель
Реализовать materialize: применить параметры к шаблону workflow, обработать файлы, seed policy и diagnostics.

## Объём работ
- `MaterializeAsync(params)`:
  - применить патчи по selectors
  - применить FileInput (upload/refs)
  - применить seed policy
  - применить runtime overrides
- Diagnostics: сохранить финальный workflow.json при включении.

## Критерии приёмки
- Materializer выдаёт готовый JSON, который можно submit.
- Diagnostics сохраняет файл.
- Ошибки информативные (`ComfyException` / `SpecValidationException`).

## Как проверить
- В sample: включить diagnostics и убедиться, что workflow.json появился.
- Проверить, что file параметры реально заменились на ссылки/имена как ожидается.
