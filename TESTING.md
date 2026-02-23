# Testing TokenLUV

## Development

```bash
npm run dev
```

La app abrirá automáticamente. Ctrl+D abre DevTools.

## Para testear con tu API key de OpenRouter

1. En la app, haz clic en ⚙️ (Settings)
2. Ingresa tu OpenRouter API key en el campo "OpenRouter API Key"
3. Haz clic en "Guardar"
4. Vuelve al Dashboard
5. Haz clic en "Actualizar ahora"
6. Deberías ver tus créditos en la card de OpenRouter

## Build para Windows

```bash
npm run build        # Compila todo
npm run dist         # Genera .exe instalable
```

El archivo `.exe` estará en `dist/`

## Estructura actual

✅ Tray icon en la bandeja (click para abrir/cerrar)
✅ Dashboard con 5 providers
✅ Settings con almacenamiento encriptado
✅ OpenRouter provider integrado
⏳ OpenAI, Anthropic, xAI, Gemini (próximos)

## Providers completados

- **OpenRouter**: ✅ Funcional (créditos USD)

## Próximas integraciones

1. OpenAI
2. Anthropic
3. xAI
4. Gemini
