# Task B2_2 — Download и выбор outputs

## Цель
Сделать выбор outputs через settings и скачивание артефактов (не только картинки).

## Объём работ
- Settings: outputs rules (types/namePatterns/mode).
- `OutputArtifact` включает type/name/url/metadata.
- Downloader умеет bytes/files.
- По умолчанию имя файла результата = GUID (если сохраняем).

## Критерии приёмки
- Если в settings указано ждать video/audio — они попадают в результат.
- Можно скачать first/all/byName.
- Redirect handling работает.
- Сохранённые файлы имеют GUID-имена (или указанную стратегию).

## Как проверить
- В sample: показать выбор outputs=images only.
- Тест: фильтрация по glob имени.
