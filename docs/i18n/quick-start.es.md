# az-ai -- Inicio rápido

> **Nota de traducción:** Este documento es una traducción de mejor esfuerzo del inglés.
> Se agradece la revisión de hablantes nativos antes de la versión v3.0.
> Si encuentras algún error, crea un Issue haciendo referencia a `s04off1-the-translation`.
> Translation note: best-effort Spanish (es). Native-speaker review wanted before v3.0.
> File an issue referencing s04off1-the-translation.

---

## Instalación

### Binarios precompilados (recomendado)

Descarga el binario para tu plataforma desde la
[página de Releases](https://github.com/SchwartzKamel/azure-openai-cli/releases).

```bash
# Ejemplo para Linux x64 (reemplaza el número de versión con el más reciente)
tar -xf az-ai-2.2.0-linux-x64.tar.gz
mv az-ai ~/.local/bin/
chmod +x ~/.local/bin/az-ai
```

Para macOS (Apple Silicon):

```bash
tar -xf az-ai-2.2.0-osx-arm64.tar.gz
mv az-ai ~/.local/bin/
chmod +x ~/.local/bin/az-ai
```

### Compilar desde el código fuente (requiere .NET 10 SDK)

```bash
git clone https://github.com/SchwartzKamel/azure-openai-cli
cd azure-openai-cli
make setup && make install
```

Verifica la instalación:

```bash
az-ai --version
```

---

## Configurar credenciales

Crea el archivo `~/.config/az-ai/env` con tus credenciales de Azure OpenAI.
Se requieren tres variables de entorno:

- `AZUREOPENAIENDPOINT` -- URL del punto de conexión de tu recurso de Azure OpenAI
- `AZUREOPENAIAPI` -- Clave de la API (nota: la variable se llama "API", no "KEY")
- `AZUREOPENAIMODEL` -- Nombre del despliegue del modelo (varios nombres separados por coma)

```bash
mkdir -p ~/.config/az-ai
cat > ~/.config/az-ai/env << 'EOF'
export AZUREOPENAIENDPOINT="https://tu-recurso.openai.azure.com/"
export AZUREOPENAIAPI="tu-clave-api-aqui"
export AZUREOPENAIMODEL="gpt-4o,gpt-4o-mini"
EOF
chmod 600 ~/.config/az-ai/env
```

Este archivo se carga automáticamente al iniciar `az-ai`.
No es necesario ejecutar `source` manualmente en el shell.

> **Consejo:** Si no hay credenciales configuradas la primera vez que ejecutas `az-ai`,
> el programa iniciará un asistente de configuración interactivo.
> Puedes volver a ejecutarlo en cualquier momento con `az-ai --setup`.

---

## Primer comando

Prueba básica:

```bash
az-ai "Hola, por favor responde en español."
```

Ejemplo para resumir el contenido de un archivo:

```bash
az-ai --raw "Resume este archivo en tres líneas, en español: $(cat README.md)"
```

El parámetro `--raw` elimina el indicador de progreso y los saltos de línea adicionales.
Es ideal para integrarlo con herramientas de expansión de texto como Espanso o AutoHotkey.

Especificar un modelo concreto:

```bash
az-ai --model gpt-4o "Dame una explicación técnica detallada."
```

---

## Próximos pasos

Este documento cubre sólo los aspectos esenciales.
Para la documentación completa, consulta el [README](../../README.md) en inglés.

Funcionalidades principales:

- `az-ai --agent "tarea"` -- modo agente con llamadas a herramientas
- `az-ai --ralph "tarea" --validate "comando"` -- bucle autónomo de autocorrección
- `az-ai --image "descripción"` -- generación de imágenes
- `az-ai --doctor` -- diagnóstico de la configuración

Guías de inicio rápido en otros idiomas: [`docs/i18n/`](README.md).

---

## Notas de traducción

Los caracteres con tilde y acento del español están presentes en este documento:
vocales con acento agudo (á, é, í, ó, ú); ñ con virgulilla; signos de interrogación
e exclamación invertidos (¿, ¡) donde corresponde. Todos son secuencias UTF-8 válidas
y pasan por la tubería de az-ai sin modificación -- la infraestructura UTF-8 existente
los maneja sin ningún tratamiento especial.

| Término original | Traducción | Nota |
|-----------------|------------|------|
| "credentials" | credenciales | Término estándar en documentación técnica en español |
| "deployment name" | nombre del despliegue | Terminología de Azure en español |
| "endpoint" | punto de conexión | Término oficial de Microsoft en español |
| "text expander" | herramienta de expansión de texto | Descripción funcional |
| "autonomous self-correcting loop" | bucle autónomo de autocorrección | [?] podría refinarse con revisión nativa |
