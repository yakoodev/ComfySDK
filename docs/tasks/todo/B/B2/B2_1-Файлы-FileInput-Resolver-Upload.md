# Task B2_1 — FileInput Resolver + Upload

## Цель
Реализовать FileInput и удобную загрузку файлов под Server/Cloud.

## Объём работ
- Тип `FileInput` (Path/Url/Base64/Bytes/Stream).
- `IFileResolver` (скачать URL, декодировать base64, открыть файл).
- `IFileUploader` стратегии (ServerUploader/CloudUploader) выбираются по options.
- Кэширование: если один и тот же `FileInput` повторно — можно не грузить (content-hash cache).

## Критерии приёмки
- Пользователь передаёт любой источник, SDK сам делает upload/refs.
- Для Server используются upload роуты, для Cloud — cloud upload.
- Повторная отправка одного и того же контента не делает лишних upload (если включён cache).

## Как проверить
- В sample: загрузить одну картинку дважды и убедиться по логам, что upload один.
- Тест: base64 → upload вызывается с bytes.
