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

El Codespace ya trae `GITHUB_TOKEN`, que sirve para [GitHub Models](https://github.com/marketplace/models). Si da error 401/403, crea un token personal con permiso **Models: read** y expórtalo: `export GITHUB_TOKEN=<tu token>`.

Opciones:

```bash
dotnet run -- ../../data/sample/gastos_ficticios.csv 10   # CSV y número de gastos
MODEL=openai/gpt-4.1 dotnet run                            # otro modelo
```

### Datos

- `data/sample/` contiene gastos **ficticios**, seguros para el repo.
- Los datos reales van en `data/real/`, que está en `.gitignore` y nunca se sube.
