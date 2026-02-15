# ComfySdk

[English](README.md) | Русский

ComfySdk - это .NET SDK для ComfyUI с:
- HTTP-клиентом (`submit`, `history`, `download`)
- материализацией workflow из `workflow.json` + `settings.*.json`
- генератором строготипизированных классов параметров
- тестовым playground для быстрого локального прогона

## Структура Проекта
- `src/ComfySdk` - runtime SDK
- `src/ComfySdk.Generator` - CLI генератор классов
- `samples/ComfySdk.Playground` - рабочий end-to-end playground
- `samples/ComfySdk.Samples` - дополнительные примеры
- `tests/*` - unit/smoke тесты

## Правило Имен Генерируемых Классов
Имя генерируемого класса:
- `{ИмяФайлаБезРасширения}Workflow`
- в C# PascalCase
- спецсимволы удаляются

Примеры:
- `default.json` -> `DefaultWorkflow` (`DefaultWorkflow.cs`)
- `my-awesome_flow.v2.json` -> `MyAwesomeFlowV2Workflow` (`MyAwesomeFlowV2Workflow.cs`)

## Быстрый Старт (минимальный рабочий сценарий)

### 1) Положить файлы сюда
- `samples/ComfySdk.Playground/Specs/Default/default.json`
- `samples/ComfySdk.Playground/Specs/Default/settings.default.json`

### 2) Запуск playground
```powershell
$env:COMFY_BASE_URL="http://127.0.0.1:8188"
$env:COMFY_SUBMIT="true"
$env:COMFY_PROMPT="china woman 2"
dotnet run -c Release --project samples/ComfySdk.Playground/ComfySdk.Playground.csproj
```

### 3) Минимум кода вызова (в вашем приложении)
```csharp
var request = new DefaultWorkflow { PositivePrompt = "china woman 2" };
var materialized = await connector.MaterializeAsync(request);
var result = await connector.SubmitAndWaitHistoryAsync(materialized.WorkflowJson, TimeSpan.FromSeconds(90));
```

## Подробный Поток Работы

### Входные Файлы
`default.json`:
- экспортированный workflow из ComfyUI
- id нод и пути input должны соответствовать `selector/path` в settings

`settings.default.json`:
- версия схемы (`settingsSchemaVersion`)
- параметры (`name/type/selector/path/default/required`)
- опциональный блок `diagnostics`

### Генерация на Этапе Сборки
`samples/ComfySdk.Playground/ComfySdk.Playground.csproj` запускает генератор до сборки:
- workflow: `Specs/Default/default.json`
- settings: `Specs/Default/settings.default.json`
- output: `Models/`

На сборке автоматически создается `Models/DefaultWorkflow.cs`.

### Выполнение
В playground:
1. читаются workflow + settings
2. создается объект сгенерированного класса
3. материализуется финальный workflow JSON (патчатся значения)
4. отправляется в ComfyUI
5. идет polling history до появления outputs

Особенности:
- fallback `history_v2` -> `history`
- поддержка object-формата outputs из ComfyUI
- короткий grace-poll после `completed=true`, если outputs появляются с задержкой

## Переменные Окружения (Playground)
- `COMFY_BASE_URL` (по умолчанию: `http://localhost:8188`)
- `COMFY_SUBMIT` (`true/1/yes`, по умолчанию: `true`)
- `COMFY_WAIT_SECONDS` (по умолчанию: `90`)
- `COMFY_PROMPT` (текст prompt)
- `COMFY_SEED` (опционально; при отсутствии генерируется автоматически)

## Сборка и Тесты
```powershell
dotnet build src/ComfySdk/ComfySdk.csproj
dotnet build src/ComfySdk.Generator/ComfySdk.Generator.csproj
dotnet build samples/ComfySdk.Playground/ComfySdk.Playground.csproj
dotnet run --project tests/ComfySdk.Tests/ComfySdk.Tests.csproj
dotnet run --project tests/ComfySdk.SmokeTests/ComfySdk.SmokeTests.csproj
```
