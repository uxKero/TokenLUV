# TokenLUV 💜

Multi-provider AI token & credit monitor para Windows. Monitorea en tiempo real el consumo de tokens y créditos de Anthropic, OpenAI, OpenRouter, xAI y Google Gemini desde la bandeja del sistema.

## Características

- 📊 Monitoreo de múltiples proveedores en un solo widget
- 🔄 Actualización automática cada 5 minutos
- 🎯 Tooltip en vivo en la bandeja
- 🔐 Las API keys se encriptan localmente
- 🌙 Dark mode por defecto
- ⚡ Totalmente open source

## Tech Stack

- **Desktop**: Electron 33+
- **Build**: Vite + electron-vite
- **UI**: React 18 + Tailwind CSS
- **Storage**: electron-store (encriptado)

## Instalación

### Desde código

```bash
npm install
npm run dev
```

### Compilar

```bash
npm run build
npm run dist  # Genera .exe
```

## Uso

1. Abre TokenLUV
2. Haz clic en ⚙️ (Configuración)
3. Ingresa tus API keys de los proveedores que uses
4. ¡Listo! El monitor se actualiza automáticamente

## Proveedores soportados (V1)

- ✅ **OpenRouter**: Créditos / límite
- ⏳ **OpenAI**: Gasto / límite del mes
- ⏳ **Anthropic**: Tokens de sesión (tracking local)
- ⏳ **xAI (Grok)**: Créditos / límite
- ⏳ **Google Gemini**: Status + request count

## Licencia

MIT
