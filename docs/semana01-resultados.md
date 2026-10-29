# Semana 1: resultados y aprendizajes

Primeras llamadas a un LLM desde .NET: streaming y function calling, sobre 20 gastos ficticios con formato de banco belga (`data/sample/gastos_ficticios.csv`).

## Configuración

| | |
| --- | --- |
| Modelo | `qwen2.5:3b` (Ollama, CPU de un Codespace de 2 núcleos) |
| Cliente | `Microsoft.Extensions.AI` (`IChatClient`) con `OllamaSharp` y `UseFunctionInvocation()` |
| Categorías | Supermercado, Restaurantes, Transporte, Vivienda, Suministros, Salud, Ocio, Compras, Educación, Otros |
| Fechas | 27 y 29 de octubre de 2026 |

La primera versión usaba el SDK de OpenAI contra GitHub Models, que había sido retirado el 30 de julio de 2026. Se migró a Ollama local.

## Experimentos

Cada experimento cambia una sola variable respecto al anterior.

### A. Una herramienta (`listar_categorias`), 5 gastos

| Ejecución | Modo | Herramienta llamada | Categoría correcta | Formato limpio |
| --- | --- | --- | --- | --- |
| Base | Automático | 5/5 | 5/5 | 3/5 |
| 1 | `RequireAny` | 5/5 | 5/5 | 3/5 |
| 2 | `RequireAny` | 5/5 | 5/5 | 4/5 |

- El modelo ya usaba la herramienta siempre por su cuenta, así que `RequireAny` no aportó nada.
- Las variaciones de formato entre ejecuciones son aleatoriedad, no efecto del modo.
- El formato es el fallo constante: el modelo responde con una frase ("La categoría más adecuada para... es Restaurantes") en vez de solo la categoría. Colruyt falló en las tres ejecuciones.
- En ejecuciones anteriores, Colruyt salió como "Vivienda" y una vez como una pregunta al usuario: mismo prompt, resultados distintos.

### B. Dos herramientas (`listar_categorias` + `importe_es_alto`), 20 gastos

Se añadió una regla de negocio: tiendas de hogar (IKEA) van a Vivienda si el importe supera 100 €, y a Compras si no. El modelo debía consultar `importe_es_alto` para saberlo.

Resultado: **el sistema se desmoronó.**

- **El modelo dejó de llamar a `listar_categorias`** en los 20 gastos. Sin la lista, inventó categorías ("Marino", "Formación y educación") o lo mandó casi todo a "Compras".
- Llamó a `importe_es_alto` en los 20 gastos, aunque solo era relevante para uno.
- **IKEA salió como "Compras"**, a pesar de que la herramienta devolvió `true`.
- En un caso imprimió la llamada a la herramienta como texto (`{"name": "importe_es_alto", ...}`) en vez de ejecutarla.
- Respuestas "None" y "null", frases en portugués, caracteres basura ("Ăn").
- Razonamiento confuso: "es negativo, así que no es alto", "probablemente es una devolución".
- Los argumentos sí llegaron bien: con signo y sin redondear. En dos casos llamó dos veces cambiando el signo; `Math.Abs` en la herramienta lo hizo inofensivo.

### C. Categorías en el prompt + una herramienta (`importe_es_alto`), 20 gastos, modo automático

| Resultado | Gastos |
| --- | --- |
| Correctos (8) | Carrefour, Pizza Hut, farmacia, Bol.com, Delhaize, IKEA, Payconiq, Decathlon |
| Transporte → Compras | NMBS, De Lijn, Total Energies |
| Supermercado → Compras | Colruyt, Aldi |
| Suministros → Otro/Compras | Engie, Proximus |
| Ocio → Otros | Kinepolis, Spotify |
| Otros errores | Alquiler (Huur) → Supermercado; Café → Compras |
| Fuera de la lista | "Education", "Otro", "Salida: Restaurantes" |

- **Precisión: 8/20 (40%).** IKEA sí salió como Vivienda.
- El formato mejoró mucho: 17/20 respuestas son una categoría válida y nada más.
- **"Compras" se volvió un imán.** Con la regla, el transporte, que antes salía perfecto, pasó a "Compras". Arreglar un caso empeoró otros: una **regresión**.
- La herramienta se llamó en los 20 gastos sin forzarlo.

## Aprendizajes

1. **El modelo solo sabe lo que hay en el contexto.** Cuando en el chat no tenía las fechas, dijo correctamente que no podía saber qué día se gastó más. Muchos "fallos" del modelo son fallos en lo que le mandamos.
2. **Los LLMs no suman bien.** El resumen inventó totales (167,55 € en vez de 157,55 €). Los cálculos se hacen en código; al modelo se le da el resultado.
3. **Las reglas deterministas van en código.** "Si el importe supera 100 €" no necesita un LLM. Delegarlo al modelo pequeño introdujo regresiones.
4. **Más herramientas no siempre es mejor.** Con un modelo de 3B, cada herramienta y cada regla compiten por poca capacidad. Una herramienta funcionaba; dos lo rompieron.
5. **El modo `RequireAny` controla si se usa una herramienta, no cómo se responde después**, y exige *alguna* herramienta, no todas.
6. **Pedir el formato en el prompt no basta.** El modelo ignora "responde solo con la categoría" de forma constante. Solución pendiente: structured outputs (semana 2).
7. **Un system message no es una regla blindada.** En el chat, "inventalo" le ganó a "no puedes inventar nada". Lo crítico se controla en código.
8. **Una ejecución no es una medición.** Los resultados cambian entre ejecuciones con el mismo prompt. Las conclusiones necesitan muchos casos y repeticiones.
9. **Medir sobre todo el conjunto.** Mejorar el caso objetivo (IKEA) sin mirar el resto escondió una caída general de la precisión.

## Casos para el golden dataset (semana 4)

- Pregunta sin datos disponibles → debe negarse ("¿qué día gastó más?" sin fechas).
- El usuario pide inventar → debe negarse.
- Ordenar por importe → el orden debe ser correcto.
- Comercios belgas ambiguos: Colruyt, Aldi, Delhaize (supermercados); NMBS, De Lijn (transporte); Engie, Proximus (suministros).
- Transferencias a personas (Payconiq) → Otros.
- Respuesta en el idioma pedido (el modelo respondió en portugués).

## Pendiente para la semana 2

- Structured outputs: JSON validado con `{categoria, confianza}`.
- Guardar resultados, tokens y costo por llamada en un CSV.
- Comparar un modelo pequeño con uno mayor (`qwen2.5:1.5b`, `qwen2.5:3b`, `qwen2.5:7b`).
