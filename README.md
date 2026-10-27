# ai-engineer-lab

Proyectos prácticos del roadmap hacia Applied AI Engineer: LLMs en .NET, evals, observabilidad, RAG, agentes e IA responsable.

## Semana 1: primeras llamadas a un LLM en .NET

App de consola que lee gastos de un CSV y:

1. **Streaming:** pide al modelo un resumen de los gastos y lo muestra token a token.
2. **Function calling:** categoriza cada gasto; el modelo llama a la función `listar_categorias` para conocer las categorías permitidas antes de responder.

### Cómo ejecutarlo (GitHub Codespaces)

1. En GitHub: **Code → Codespaces → Create codespace on main**. La primera vez tarda unos minutos.
2. En la terminal del Codespace:

   ```bash
   cd src/Week01
   dotnet run
   ```

### Proveedor del modelo

La app usa cualquier API compatible con OpenAI, configurada con variables de entorno:

| Variable | Qué es |
| --- | --- |
| `LLM_BASE_URL` | Endpoint del proveedor. Vacío = API oficial de OpenAI. |
| `LLM_API_KEY` | Clave del proveedor. |
| `MODEL` | Nombre del modelo en ese proveedor. |

Ejemplos:

```bash
# Ollama (local, gratis)
export LLM_BASE_URL=http://localhost:11434/v1 LLM_API_KEY=ollama MODEL=qwen2.5:3b

# Microsoft Foundry (endpoint v1 compatible con OpenAI)
export LLM_BASE_URL=https://<tu-recurso>.openai.azure.com/openai/v1/ LLM_API_KEY=<clave> MODEL=<nombre-del-deployment>

# OpenAI
export LLM_API_KEY=<clave> MODEL=gpt-4.1-mini
```

Opciones:

```bash
dotnet run -- ../../data/sample/gastos_ficticios.csv 10   # CSV y número de gastos
```

Las claves nunca van en el repo: usa `export` en la terminal o los secretos de Codespaces.

### Datos

- `data/sample/` contiene gastos **ficticios**, seguros para el repo.
- Los datos reales van en `data/real/`, que está en `.gitignore` y nunca se sube.
