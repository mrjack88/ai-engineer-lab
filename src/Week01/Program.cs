// Semana 1: primeras llamadas a un LLM desde .NET.
// 1) Streaming: el modelo responde token a token.
// 2) Function calling: el modelo pide ejecutar una función nuestra antes de responder.
//
// Uso:
//   dotnet run -- [ruta_csv] [n_gastos]
// Por defecto usa data/sample/gastos_ficticios.csv y 5 gastos.

using System.ClientModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OllamaSharp;

// Proveedor configurable por variables de entorno (cualquier API compatible con OpenAI):
//   LLM_BASE_URL  -> p. ej. http://localhost:11434/v1 (Ollama) o el endpoint v1 de Microsoft Foundry.
//                    Vacío = API oficial de OpenAI.
//   LLM_API_KEY   -> la clave del proveedor (Ollama acepta cualquier texto).
//   MODEL         -> nombre del modelo en ese proveedor.
var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL") ?? "http://localhost:11434";
var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY")
    ?? throw new InvalidOperationException("Falta la variable de entorno LLM_API_KEY.");
var model = Environment.GetEnvironmentVariable("MODEL")
    ?? throw new InvalidOperationException("Falta la variable de entorno MODEL.");
var csvPath = args.Length > 0 ? args[0] : FindSampleCsv();
var maxGastos = args.Length > 1 ? int.Parse(args[1]) : 20;

var client = CreateChatClient(baseUrl, model);

var gastos = LeerGastos(csvPath).Take(maxGastos).ToList();
Console.WriteLine($"Modelo: {model} | {gastos.Count} gastos de {csvPath}\n");

// ---------- 1) Streaming ----------
// Console.WriteLine("== 1) Streaming ==");
// var resumenPrompt = "Resume en 3 frases en qué se gasta el dinero esta persona:\n"
//     + string.Join("\n", gastos.Select(g => $"- {g.Descripcion}: {g.Importe:0.00} EUR"));

// // Initialize conversation with a system message
// List<ChatMessage> conversation = new()
// {
//     new(ChatRole.System, "Eres un asistente de finanzas personales. Responde siempre en español. Tu rol es responder preguntas relacionadas con gastos y finanzas. No puedes inventar nada."),
//     new ChatMessage(ChatRole.User, resumenPrompt)
// };

// var updates = new List<ChatResponseUpdate>();
// await foreach (var update in client.GetStreamingResponseAsync(conversation))
// {
//     Console.Write(update.Text);
//     updates.Add(update);
// }
// conversation.AddMessages(updates); 
// Console.WriteLine("\n");

// // Intereactive streaming part
// while(true)
// {
//     Console.Write("Tu: ");
//     var question = Console.ReadLine();
//     if (string.IsNullOrWhiteSpace(question)) continue;
//     if (question.Trim().Equals("salir", StringComparison.OrdinalIgnoreCase)) break;

//     conversation.Add(new ChatMessage(ChatRole.User, question));
//     Console.Write("Modelo: ");
//     await foreach (var update in client.GetStreamingResponseAsync(conversation))
//     {
//         Console.Write(update.Text);
//         updates.Add(update);
//     }
//     conversation.AddMessages(updates); 
//     Console.WriteLine("\n");
// }

// ---------- 2) Function calling ----------
Console.WriteLine("== 2) Function calling ==");
string[] categorias = ["Supermercado", "Restaurantes", "Transporte", "Vivienda", "Suministros",
                       "Salud", "Ocio", "Compras", "Educación", "Otros"];

var listarCategorias = AIFunctionFactory.Create(
    () =>
    {
        Console.WriteLine("[herramienta] listar_categorias");
        return categorias;
    },
    name: "listar_categorias",
    description: "Devuelve la lista cerrada de categorías de gasto permitidas.");

var importe_es_alto = AIFunctionFactory.Create(
    ([Description("Importe del gasto en euros")] decimal importe) =>
    {
        Console.WriteLine($"[herramienta] importe_es_alto ({importe})");
        return Math.Abs(importe) > 100;
    },
    name: "importe_es_alto",
    description: "Recibe el importe de un gasto y responde si es “alto”, es decir, si supera 100 € en valor absoluto.");

var options = new ChatOptions
{
    Tools = [importe_es_alto]
};

foreach (var gasto in gastos)
{
    var messages = new List<ChatMessage>();
    messages.Add(new ChatMessage(ChatRole.System,
        "Categorizas gastos bancarios. Estas son las categorias disponibles: Supermercado, Restaurantes, Transporte, Vivienda, Suministros, Salud, Ocio, Compras, Educación, Otros. " +
        "Responde solo con el nombre exacto de una categoría. " + 
        "Las compras en tiendas de hogar, muebles o bricolaje (IKEA, por ejemplo) van a Vivienda si el importe es alto, y a Compras si no lo es. " +
        "Para saber si el importe es alto, utiliza la herramienta importe_es_alto"));
    messages.Add(new ChatMessage(ChatRole.User,
        $"Gasto: \"{gasto.Descripcion}\", importe {gasto.Importe:0.00} EUR"));


    ChatResponse response = await client.GetResponseAsync(messages, options);
    Console.WriteLine($"{gasto.Descripcion,-40} -> {response.Text.Trim()}");
}

// ---------- utilidades ----------
static IChatClient CreateChatClient(string baseUrl, string model)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
    ArgumentException.ThrowIfNullOrWhiteSpace(model);

    IChatClient ollamaClient = new OllamaApiClient(
        new Uri(baseUrl), model);

    return new ChatClientBuilder(ollamaClient)
        //avoids the  model to be involved in an infite loop calling tools
        .UseFunctionInvocation(configure: f => f.MaximumIterationsPerRequest = 5)
        .Build();
}

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
