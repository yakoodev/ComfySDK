# Task B1_2 — SeedPolicy и overrides

## Цель
Сделать удобную политику seed и runtime overrides (без ломания генератора).

## Объём работ
- `SeedPolicy`: Fixed, Random, FromValue.
- В materializer применять seed policy к целевому input.
- Overrides: возможность подменить отдельные параметры без правки settings (runtime only).

## Критерии приёмки
- Seed можно задать через Params.
- Random seed генерится на каждый run.
- Overrides применяются после materialize и перед submit.

## Как проверить
- В sample: 2 run подряд с Random seed → разные seed в diagnostics json.
