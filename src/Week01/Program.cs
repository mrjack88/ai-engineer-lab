// Semana 1: primeras llamadas a un LLM desde .NET.
// 1) Streaming: el modelo responde token a token.
// 2) Function calling: el modelo pide ejecutar una función nuestra antes de responder.
//
// Uso:
//   dotnet run -- [ruta_csv] [n_gastos]
// Por defecto usa data/sample/gastos_ficticios.csv y 5 gastos.

using System.ClientModel;
using System.Globalization;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;

// Proveedor configurable por variables de entorno (cualquier API compatible con OpenAI):
//   LLM_BASE_URL  -> p. ej. http://localhost:11434/v1 (Ollama) o el endpoint v1 de Microsoft Foundry.
//                    Vacío = API oficial de OpenAI.
//   LLM_API_KEY   -> la clave del proveedor (Ollama acepta cualquier texto).
//   MODEL         -> nombre del modelo en ese proveedor.
var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL");
var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY")
    ?? throw new InvalidOperationException("Falta la variable de entorno LLM_API_KEY.");
var model = Environment.GetEnvironmentVariable("MODEL")
    ?? throw new InvalidOperationException("Falta la variable de entorno MODEL.");
var csvPath = args.Length > 0 ? args[0] : FindSampleCsv();
var maxGastos = args.Length > 1 ? int.Parse(args[1]) : 5;

var clientOptions = new OpenAIClientOptions();
if (!string.IsNullOrWhiteSpace(baseUrl)) clientOptions.Endpoint = new Uri(baseUrl);
var client = new ChatClient(model, new ApiKeyCredential(apiKey), clientOptions);

var gastos = LeerGastos(csvPath).Take(maxGastos).ToList();
Console.WriteLine($"Modelo: {model} | {gastos.Count} gastos de {csvPath}\n");

// ---------- 1) Streaming ----------
Console.WriteLine("== 1) Streaming ==");
var resumenPrompt = "Resume en 3 frases en qué se gasta el dinero esta persona:\n"
    + string.Join("\n", gastos.Select(g => $"- {g.Descripcion}: {g.Importe:0.00} EUR"));

await foreach (var update in client.CompleteChatStreamingAsync([new UserChatMessage(resumenPrompt)]))
{
    foreach (var part in update.ContentUpdate)
        Console.Write(part.Text);
}
Console.WriteLine("\n");

// ---------- 2) Function calling ----------
Console.WriteLine("== 2) Function calling ==");
string[] categorias = ["Supermercado", "Restaurantes", "Transporte", "Vivienda", "Suministros",
                       "Salud", "Ocio", "Compras", "Educación", "Otros"];

var listarCategorias = ChatTool.CreateFunctionTool(
    functionName: "listar_categorias",
    functionDescription: "Devuelve la lista cerrada de categorías de gasto permitidas.");

var options = new ChatCompletionOptions { Tools = { listarCategorias } };

foreach (var gasto in gastos)
{
    List<ChatMessage> messages =
    [
        new SystemChatMessage(
            "Categorizas gastos bancarios. Usa siempre la herramienta listar_categorias " +
            "y responde solo con el nombre exacto de una categoría."),
        new UserChatMessage($"Gasto: \"{gasto.Descripcion}\", importe {gasto.Importe:0.00} EUR")
    ];

    // Bucle de herramientas: el modelo puede pedir funciones varias veces antes de responder.
    for (var paso = 0; paso < 5; paso++)
    {
        ChatCompletion completion = await client.CompleteChatAsync(messages, options);

        if (completion.FinishReason == ChatFinishReason.ToolCalls)
        {
            messages.Add(new AssistantChatMessage(completion));
            foreach (var call in completion.ToolCalls)
            {
                var resultado = call.FunctionName == "listar_categorias"
                    ? JsonSerializer.Serialize(categorias)
                    : "{\"error\":\"función desconocida\"}";
                Console.WriteLine($"  [herramienta] {call.FunctionName}");
                messages.Add(new ToolChatMessage(call.Id, resultado));
            }
            continue;
        }

        var u = completion.Usage;
        Console.WriteLine($"{gasto.Descripcion,-40} -> {completion.Content[0].Text.Trim()}" +
                          $"   (tokens: {u.InputTokenCount} in / {u.OutputTokenCount} out)");
        break;
    }
}

// ---------- utilidades ----------
static IEnumerable<Gasto> LeerGastos(string path)
{
    // Formato típico de banco belga: separador ';', decimales con coma, fecha dd/MM/yyyy.
    var es = CultureInfo.GetCultureInfo("es-ES");
    foreach (var line in File.ReadLines(path).Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        var c = line.Split(';');
        yield return new Gasto(
            DateTime.ParseExact(c[0].Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture),
            c[1].Trim(),
            decimal.Parse(c[2].Trim(), es));
    }
}

static string FindSampleCsv()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "data", "sample", "gastos_ficticios.csv");
        if (File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    throw new FileNotFoundException("No encuentro data/sample/gastos_ficticios.csv");
}

record Gasto(DateTime Fecha, string Descripcion, decimal Importe);
