#!/usr/bin/env bash
# Prepara Ollama en el Codespace y deja configuradas las variables para la app.
# Uso (con "source" para que las variables queden en tu terminal):
#   source scripts/ollama.sh            # modelo por defecto
#   source scripts/ollama.sh qwen2.5:1.5b   # otro modelo

OLLAMA_MODEL="${1:-qwen2.5:3b}"

# 1) Instalar si no está
if ! command -v ollama >/dev/null 2>&1; then
  echo ">> Instalando Ollama..."
  curl -fsSL https://ollama.com/install.sh | sh
fi

# 2) Arrancar el servidor si no responde (en el contenedor no hay systemd)
if ! curl -s http://localhost:11434/api/version >/dev/null 2>&1; then
  echo ">> Arrancando el servidor de Ollama..."
  nohup ollama serve >/tmp/ollama.log 2>&1 &
  for _ in $(seq 1 30); do
    curl -s http://localhost:11434/api/version >/dev/null 2>&1 && break
    sleep 1
  done
fi

# 3) Descargar el modelo (solo la primera vez)
echo ">> Descargando el modelo $OLLAMA_MODEL (si no está ya)..."
ollama pull "$OLLAMA_MODEL"

# 4) Variables para la app
export LLM_BASE_URL=http://localhost:11434/v1
export LLM_API_KEY=ollama
export MODEL="$OLLAMA_MODEL"
echo ">> Listo. LLM_BASE_URL=$LLM_BASE_URL MODEL=$MODEL"
