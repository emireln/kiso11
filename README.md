# Kiso11

> **Windows ISO Customizer & Bootable USB Creator**<br>
> *Uma ferramenta desktop simples para selecionar uma edição, personalizar a imagem e criar mídia de instalação.*

[![Repository](https://img.shields.io/badge/GitHub-emireln%2Fkiso11-181717?style=for-the-badge&logo=github)](https://github.com/emireln/kiso11)
[![Fork of](https://img.shields.io/badge/Fork%20of-itsNileshHere%2FWindows--ISO--Debloater-gray?style=for-the-badge&logo=github)](https://github.com/itsNileshHere/Windows-ISO-Debloater)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue?style=for-the-badge&logo=windows)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-8.0%20WPF-512bd4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/License-GPL--3.0-green?style=for-the-badge)](LICENSE)

---

> ℹ️ **Sobre este projeto**: O **Kiso11** ([github.com/emireln/kiso11](https://github.com/emireln/kiso11)) é um fork do [Windows-ISO-Debloater](https://github.com/itsNileshHere/Windows-ISO-Debloater), desenvolvido originalmente por [itsNileshHere](https://github.com/itsNileshHere). Esta versão inclui um aplicativo desktop C# (.NET 8 WPF), seleção de edição e criação de mídia UEFI a partir de imagens `install.wim`, `install.esd` e `install.swm`.

---

Este aplicativo foi desenvolvido enquanto eu estudava C# e com auxílio de IA (Muse Spark 1.3). Já havia desenvolvido a ferramenta ETT em 2024 usando Python e C#. Usei a base para fazer o Kiso11 e também é uma fork.

## Visão Geral

O **Kiso11** é uma solução completa para otimizar, despoluir (*debloat*) e personalizar imagens de instalação do Windows 11 e 10. Ele remove bloatware de fábrica, componentes intrusivos de IA (Copilot, Recall), telemetria e restrições artificiais de hardware, permitindo gerar uma **nova ISO debloated** ou gravar diretamente um **Pendrive Bootável UEFI** pronto para uso.

Diferente de scripts complicados ou ferramentas com interfaces web inchadas, o Kiso11 possui um aplicativo nativo em **C# (.NET 8 WPF)** com fluxo guiado, opções descritas com clareza e acompanhamento do processo em tempo real.

---

## Recursos Principais

### Aplicativo Desktop em C# (.NET 8 WPF)
- **Fluxo guiado em 4 etapas**: Escolha da ISO e edição, personalização, destino e execução.
- **Interface desktop própria**: Barra de título integrada ao Windows, controles de janela padrão, hierarquia visual simples e ações principais em botões grandes.
- **Bilingue (PT-BR & EN)**: Alternância instantânea de idioma com 1 clique diretamente no cabeçalho.
- **Bandeja do Sistema (System Tray)**: Botão dedicado para minimizar para a área de notificação do Windows com suporte a restauração por duplo clique e menu de contexto.
- **Monitoramento em Tempo Real**:
  - Barra de progresso com porcentagem dinâmica por etapa.
  - Cronômetro de tempo decorrido e estimativa de tempo restante.
  - Console de logs detalhado ao vivo com cópia e limpeza em 1 clique.
- **Cancelamento Seguro**: Permite interromper o processo limpando e desmontando as imagens WIM sem corromper arquivos ou deixar resíduos no disco.

### 💾 Criação de Pendrive Bootável Integrada
- **Detecção Segura**: Identifica automaticamente discos removíveis USB, filtrando unidades do sistema para evitar acidentes.
- **Mídia UEFI em FAT32**: Cria mídia de instalação FAT32 e divide imagens WIM grandes para caber no limite de arquivo do sistema. A compatibilidade final depende do firmware e do hardware.
- **Split-Image Automático**: Se o `install.wim` exceder o limite de arquivo do FAT32, o Kiso11 o divide em partes `.swm` via DISM.
- **Unidades maiores que 32 GB**: Como o formatador FAT32 do Windows limita o tamanho do volume, é criada uma partição de inicialização FAT32 de 32 GB; o conteúdo restante do disco fica sem partição.
- **Alternativa em Arquivo ISO**: Possibilidade de salvar como arquivo `.iso` inicializável gerado via `oscdimg` (com download transparente caso não esteja instalado).

### 🛡️ Opções de personalização do Windows
- **Criptografia automática do dispositivo**: Define `PreventDeviceEncryption` para impedir a criptografia automática durante a instalação quando a imagem oferece esse comportamento.
- **Remoção de IA e Copilot**: Remove pacotes provisionados do Copilot, Recall e bibliotecas do subsistema de IA (`AIX`, `CoreAI`).
- **Bypass de Requisitos de Hardware**: Injeta regras `LabConfig` no `install.wim` e `boot.wim` (WinPE Setup) para contornar checagens de TPM 2.0, Secure Boot, CPU e RAM em computadores mais antigos.
- **Bypass de Conta Microsoft**: Configura o OOBE (`BypassNRO`) e embute `autounattend.xml` para permitir instalação com Conta Local tradicional sem exigir internet.
- **Remoção Seletiva de Bloatware**: Remove dezenas de apps promocionais (Bing News, Clipchamp, Jogos, etc.), preservando ferramentas essenciais (Windows Defender, Calculadora, Fotos, Terminal, Bloco de Notas).
- **Integração de Drivers Intel RST / VMD**: Adiciona os drivers ao Windows Setup e à imagem da edição selecionada para reconhecimento de armazenamento durante a instalação e após ela.
- **Seleção da edição**: Lê as edições presentes em `install.wim`, `install.esd` ou `install.swm`. A mídia gerada inclui somente a edição escolhida.

---

## 📁 Estrutura do Repositório

```text
Kiso11/
├── bin/
│   └── Release/
│       └── Kiso11/            # Binário executável compilado (.exe)
├── Drivers/
│   ├── RAID/                  # Drivers Intel RST
│   ├── VMD/                   # Drivers Intel VMD para SSDs NVMe
│   └── README.md
├── scripts/
│   ├── Kiso11-Engine.ps1      # Motor de execução PowerShell autônomo
│   └── autounattend.xml       # Arquivo de resposta para bypass OOBE
├── src/
│   └── Kiso11/                # Código-fonte da aplicação C# WPF (.NET 8)
│       ├── Assets/            # Vetores XAML dedicados
│       ├── Converters/        # Conversores de dados MVVM
│       ├── Models/            # Modelos de dados e opções
│       ├── Services/          # Motores DISM, ISO, USB e Oscdimg
│       ├── Themes/            # Estilos Fluent e Dark Mode
│       ├── ViewModels/        # Lógica de controle e timers
│       ├── Views/             # Interface gráfica principal
│       └── app.manifest       # Manifest de elevação UAC
├── LICENSE                    # Licença GPL-3.0
└── README.md                  # Documentação do projeto
```

---

## 🚀 Como Usar

### Opção 1: Aplicativo Gráfico (Recomendado)

1. Execute o **`Kiso11.exe`** (localizado em `bin/Release/Kiso11/Kiso11.exe` ou compile pelo código-fonte).
2. O aplicativo solicitará elevação de **Administrador** (necessária para operações DISM e gravação de disco).
3. **Passo 1**: Selecione o arquivo `.iso` do Windows (arraste o arquivo para a janela ou clique em *Procurar ISO*).
4. **Passo 2**: Use o **Preset Recomendado** ou escolha as opções de otimização.
5. **Passo 3**: Escolha como salvar:
   - *Gerar Arquivo ISO*: Define o caminho do novo arquivo `.iso` otimizado.
   - *Gravar em Pendrive Bootável*: Selecione a unidade USB desejada. O conteúdo atual dessa unidade será apagado.
6. Revise a seleção e clique em **Iniciar Otimização**. Acompanhe o progresso e os logs durante a execução.

### Opção 2: Compilando a partir do Código-Fonte

Certifique-se de ter o [.NET 8 SDK](https://dotnet.microsoft.com/download) ou superior instalado. O aplicativo usa DISM, PowerShell e Robocopy do Windows. O DISM precisa oferecer suporte à versão da imagem escolhida; imagens mais recentes podem exigir um Windows ADK mais novo. Consulte a [matriz de compatibilidade do DISM](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-supported-platforms?view=windows-11).

```powershell
# Restaurar dependências e compilar
dotnet build src/Kiso11/Kiso11.csproj -c Release

# Executar aplicação
dotnet run --project src/Kiso11/Kiso11.csproj
```

### Opção 3: Executando via PowerShell (CLI Engine)

Para ambientes de linha de comando ou automação:

```powershell
# Execução interativa com prompts
.\scripts\Kiso11-Engine.ps1

# Execução automatizada silenciosa
.\scripts\Kiso11-Engine.ps1 -noPrompt -isoPath "C:\ISOs\Win11.iso" -winEdition "Windows 11 Pro" -outputISO "Win11_Debloated.iso"
```

---

## ⚙️ O que é Otimizado e Removido

| Componente | Ação Realizada |
| :--- | :--- |
| **Apps de Terceiros / Bloat** | Remoção de Candy Crush, TikTok, Spotify, Disney+, Clipchamp, etc. |
| **IA & Copilot / Recall** | Remoção de pacotes `Copilot`, `Recall`, `AIX`, `CoreAI` e chaves de telemetria. |
| **Criptografia automática** | Define `PreventDeviceEncryption = 1` para desativar a criptografia automática do dispositivo durante a instalação. |
| **Requisitos de Instalação** | Bypasses de TPM 2.0, Secure Boot, RAM e CPU via `LabConfig` no instalador. |
| **Conta Microsoft (OOBE)** | Habilita `BypassNRO` para permitir criação de Conta Local sem internet. |
| **Telemetria** | Desativação de relatórios de diagnóstico, ID de anúncios e sugestões no Iniciar. |
| **Drivers de Armazenamento** | Integração opcional de drivers Intel VMD/RST para reconhecimento imediato de SSDs. |

---

## 🌟 Origem & Créditos

- **Repositório Oficial Kiso11**: [https://github.com/emireln/kiso11](https://github.com/emireln/kiso11)
- **Fork do Projeto Original**: [Windows-ISO-Debloater](https://github.com/itsNileshHere/Windows-ISO-Debloater) por [itsNileshHere](https://github.com/itsNileshHere)
- Inspirações e ferramentas:
  - [tiny11builder](https://github.com/ntdevlabs/tiny11builder) (conceito de debloat de imagens de instalação)
  - [Winaero](https://winaero.com/) (técnicas de otimização de chaves de registro)
  - [RemoveWindowsAI](https://github.com/zoicware/RemoveWindowsAI) (pesquisa de remoção de componentes de IA)
  - Microsoft Windows ADK (ferramentas oficiais `dism.exe` e `oscdimg.exe`)

---

## 📜 Licença

Este projeto é distribuído sob os termos da licença [GPL-3.0](LICENSE).
