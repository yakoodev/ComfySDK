# ComfySdk — подробное ТЗ (v1.1) + дизайн, расширяемость и допущения

> Вайб: “умный клиент”, который снимает рутину (файлы, патчи, прогресс, скачивание), но не превращается в комбайн, который потом никто не заведёт.
>  
> И да: сделано так, чтобы пользователь не нейро‑страИвался, а просто генерил 😼

---

## 0. Термины
- **ComfyUI Server** — self-hosted ComfyUI с HTTP API + WS.
- **Comfy Cloud** — облачный вариант с `/api/*`, возможными редиректами на `view`, и другими версиями history.
- **Workflow** — JSON workflow (узлы, `class_type`, `inputs`).
- **Settings Spec** — JSON, описывающий параметры, селекторы узлов, патчи, outputs, политику файлов.
- **Generated Params Class** — типизированный класс параметров под конкретный workflow/settings.
- **Materializer** — превращает Params → “готовый workflow JSON”.
- **Run** — один запуск генерации (один submit), с прогрессом и результатами.

---

## 1. Цели и область
### 1.1 Что должно уметь SDK
1) **Server + Cloud из коробки**
- Поддержка настраиваемых маршрутов/префиксов (`ApiPrefix`, `RouteMap`) — чтобы не хардкодить `/history` навечно.
- `view`-скачивание с **follow redirects** (важно для Cloud).

2) **Параллельные запуски**
- Возможность запускать несколько `RunHandle` одновременно (в одном процессе).
- Без глобальных статиков, всё через DI/инстансы.

3) **Файлы**
- Единый тип `FileInput`:
  - `Path(string)`
  - `Url(Uri)`
  - `Base64(string)`
  - `Bytes(byte[])`
  - `Stream(Stream, filename?)`
- SDK сам приводит к формату загрузки конкретного endpoint’а (Server vs Cloud).

4) **Outputs по настройкам**
- В `settings.<workflow>.json` задаётся, что скачиваем:
  - по типу (image/video/audio/any)
  - по имени/маске
  - “первый/все”
  - формат выдачи: файлы на диск / bytes / stream (на выбор в runtime).

5) **Автогенерация параметров**
- `workflow.json + settings.<workflow>.json` → класс `WorkflowNameParams`.
- В классах **нет** `RunAsync`. Только параметры + метаданные.
- Выполнение делается через runtime: `client.RunAsync(params)`.

### 1.2 Не делаем в v1
- Не пишем UI.
- Не пишем editor/визуальный конструктор settings.
- Не пытаемся “универсально починить любой workflow” — если селекторы не нашли узлы, это ошибка на генерации.

---

## 2. Архитектура и пакеты
### 2.1 Пакеты
- **ComfySdk** (runtime)
  - `ComfyClient`, `RunHandle`, `RunEvent`, `RunResult`
  - `IFileUploader`, `IFileResolver`, `IDownloader`
  - `SettingsSpec` модели (общие) + валидатор
  - `Materializer`
  - Exceptions + diagnostics
- **ComfySdk.Generator**
  - CLI `comfysdk-gen` (или dotnet tool)
  - Генерит .cs файлы параметров
  - Валидирует workflow/settings (и падает, если всё плохо)
- **ComfySdk.Samples**
  - Примеры Server/Cloud
  - Пример параллельных запусков
  - Пример FileInput и output selection

### 2.2 DI и конфиг
- Всё конфигурируется через `ComfyClientOptions` + DI:
  - BaseUrl
  - ApiPrefix/RouteMap
  - Auth provider
  - Http/WS timeouts
  - Retry policy
  - Logging options (masking)

Секреты **не** должны требовать хранения в settings. “Как правильно”:
- по умолчанию — берём из ENV (`COMFY_TOKEN`, `COMFY_API_KEY`, и т.п.)
- или из user-secrets/конфиг провайдера приложения
- `settings.*.json` — только про workflow/патчи/outputs (без токенов).

---

## 3. Endpoint/Routes и различия Server/Cloud
### 3.1 RouteMap
Вводим `RouteMap` (или `ApiPrefix` + стандартные пути):
- Submit prompt
- History (v1 / v2)
- View / Download
- Upload image/mask
- Queue / Status
- Interrupt (опционально в API, но runtime может уметь дернуть при реальном стопе)

Это избавляет от “а у меня Cloud и /api”.

### 3.2 Redirect handling
Downloader обязан следовать 302/307/308 при скачивании outputs.

---

## 4. WebSocket и прогресс
### 4.1 Нормализация событий
WS сообщения приводим к `RunEvent`:
- `Connected`, `Reconnected`, `Disconnected`
- `Queued`
- `Executing(node?)`
- `Progress(value?)`
- `Succeeded`
- `Failed(error)`
- `NodeUiUpdated` (если пришло)
- `Log/Debug` (опционально, если пришло)

### 4.2 Правильное завершение
“Делаем правильно”:
- Приоритет: WS `execution_success` / `execution_error` / `execution_interrupted`.
- Фоллбек: если WS отвалился — опрашиваем history до terminal state.

### 4.3 WS reconnect
- Автопереподключение с backoff (без бесконечной долбёжки как дятел в бетон 🪵🧠).
- После reconnect продолжаем слушать по `prompt_id`.

### 4.4 Cancel
По твоему решению: **при отмене просто перестаём ждать**:
- закрываем WS
- прекращаем polling history
- **не** вызываем `/interrupt` автоматически (но API может позволять явный `StopRemoteAsync()`).

---

## 5. Workflow patching: селекторы, path и валидация
### 5.1 Селектор — главный
`NodeSelector` ищет узлы по:
- `class_type` (обязательно)
- условиям на inputs (существование/значение)
- index (если разрешено; в v1 по умолчанию запрещаем)
- `requireSingle=true` — если матчей != 1 → ошибка

По твоему решению: если матчей > 1 — **хуячим ошибку** (без “ну я выбрал первый, честно‑честно”).

### 5.2 Path формат (удобный и не слишком сложный)
Поддерживаем:
- dot-path: `inputs.seed`
- массивы: `inputs[0].image`  
Этого достаточно 99% людей и не превращает settings в китайскую грамоту.

(Если понадобится JSON Pointer — добавим в v1.2, но сейчас не усложняем.)

### 5.3 Валидация на этапе генерации
Генератор:
- парсит workflow
- применяет селекторы
- проверяет path’и
- проверяет типы
- если хоть один patch не применим → **ошибка генерации с понятным сообщением** (какой селектор, сколько матчей, какой path не найден).

---

## 6. Типы параметров и FileInput
### 6.1 Базовые типы
- string, int, double, bool
- enum (как string)
- optional (nullable)
- array/list

### 6.2 FileInput как first-class тип
Если параметр объявлен как `file`, SDK принимает:
- path / url / base64 / bytes / stream
и сам превращает в то, что нужно ноде (через upload или ссылку, зависит от endpoint).

---

## 7. Outputs: что ждём и что скачиваем
В `settings.<workflow>.json`:
- `outputs`:
  - `mode`: `all` / `first` / `byName`
  - `types`: `image|video|audio|any`
  - `namePatterns`: glob/regex (v1 — glob, regex опционально)
  - `download`: `none|bytes|files`
  - `saveDir`: default путь (если files)
  - `fileName`: `guid` (по твоему решению для результата — GUID)

В runtime:
- `RunResult` содержит:
  - список outputs (metadata)
  - опционально bytes/paths в зависимости от режима.

---

## 8. Retry, timeouts, HTTP
- Retry только для сетевых/429/5xx, с jittered backoff.
- Upload/download — отдельные таймауты.
- Все запросы логируются, но секреты маскируются.

---

## 9. Ошибки и диагностика
### 9.1 ComfyException
Единый тип исключения с полями:
- `HttpStatus`, `Route`, `RequestId`, `PromptId`
- `BodySnippet` (без секретов)
- `NodeErrors` (если есть)

### 9.2 Diagnostics mode
Опция `Diagnostics`:
- сохраняет “финальный workflow.json” после materialize в папку (temp/выбранную)
- сохраняет `settings snapshot`
- удобно для “почему оно не генерит, мать его”.

---

## 10. Генератор кода
### 10.1 Входные файлы
- `workflow.<name>.json`
- `settings.<name>.json`

### 10.2 Выход
- `Generated/<name>/<name>Params.cs`
- (опционально) `Generated/<name>/<name>Spec.cs` (метаданные)

Имена — **по имени файлов**.

### 10.3 Версионирование схемы settings
Если не сложно — делаем:
- `settingsSchemaVersion: 1`
- при несовпадении — понятная ошибка.

---

## 11. Технологические решения
- .NET 10
- `System.Text.Json`
- `Microsoft.Extensions.Logging`
- Минимум рефлексии (чтобы не болело, если захочешь AOT позже).

---

## 12. Допущения (фиксируем)
- Workflow экспортирован “как есть” из ComfyUI и содержит стабильные `class_type`.
- Settings Spec пишет человек/генератор, и он должен пройти генераторную валидацию.
- Cloud может менять детали маршрутов — поэтому RouteMap/Prefix обязателен.

---

## 13. Мини‑пример (псевдо)
```csharp
var client = services.GetRequiredService<ComfyClient>();

var p = new MyWorkflowParams {
    Prompt = "cat",
    Seed = SeedPolicy.Random(),
    InputImage = FileInput.FromUrl(new Uri("https://.../img.png"))
};

await foreach (var ev in client.RunStreamAsync(p, ct))
    Console.WriteLine(ev);

var result = await client.RunAsync(p, ct);
foreach (var o in result.Outputs)
    Console.WriteLine($"{o.Type} {o.Name} {o.Path}");
```

---

## 14. Что должен уметь агент по задачам
См. `tasks/todo/**` — там критерии приёмки и как проверять.

