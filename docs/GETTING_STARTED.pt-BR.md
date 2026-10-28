# Primeiros passos

Languages: [English](GETTING_STARTED.md) | Português

Como rodar o servidor no seu computador e jogar nele. É um servidor de teste da comunidade, não o jogo oficial.

## O que você precisa

| Item | Observações |
|---|---|
| Windows 10 ou 11 | O cliente do jogo só roda no Windows |
| Sua própria cópia do cliente do Sphere | Instale pela fonte oficial. Nós não distribuímos o cliente |
| .NET 9 SDK | A versão exata está no `global.json`. Confira com `dotnet --version` |
| Godot 4.7 Mono (ou mais novo) | Necessário para rodar e compilar o servidor. Quem só entra para jogar não precisa |
| Git | Para baixar e atualizar o código |
| Os arquivos do jogo | Dados do jogo e arquivos do mundo que o servidor precisa. Não estão neste repositório; o passo 2 os busca no repositório público do projeto original |

## 1. Baixe o código

```bash
git clone https://github.com/mathbrz/SphereEmu-BR.git
cd SphereEmu-BR
```

O repositório é público. Para contribuir, faça um fork primeiro (veja [CONTRIBUTING.pt-BR.md](../CONTRIBUTING.pt-BR.md)).

## 2. Busque os arquivos do jogo

```powershell
powershell -ExecutionPolicy Bypass -File Tools\setup-assets.ps1
```

O script baixa cerca de 3,8 GB do repositório público do projeto original (knelse/SphereEmu), na versão exata em que
este código se baseia, e coloca `Godot/Models`, `Godot/Textures`, `Godot/Terrain`, `Godot/World`,
`Sphere.GameDataDecode`, `.generated` e `sphereclient_patched.exe` ao lado de `Server/` e `Client/`.
Este projeto não hospeda nenhum desses arquivos. Não faça commit deles: o `.gitignore` já os deixa de fora. Se o
repositório original sumir ou ficar privado um dia, o script não consegue alcançá-lo. Guarde um clone completo dele (`git clone --mirror`) ou uma cópia do que o script baixou, e aponte o script para essa cópia com `-Source <caminho ou URL>` (a variável de ambiente `SPHEREEMU_ASSETS_SOURCE` faz o mesmo). Com os arquivos no lugar, o servidor nunca mais precisa desse repositório.

Na primeira vez que o servidor roda, o Godot importa os modelos e as texturas, o que pode levar vários minutos.

## 3. Compile e rode o servidor

```bash
dotnet build SphServer.sln
```

O jeito mais fácil é o script, que acha o Godot, sobe o servidor sem janela e espera até ele ficar pronto:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\server.ps1 start -Build   # compila e liga
powershell -ExecutionPolicy Bypass -File Tools\server.ps1 status        # está rodando? quantos jogadores?
powershell -ExecutionPolicy Bypass -File Tools\server.ps1 restart -Build # depois de mudar código
powershell -ExecutionPolicy Bypass -File Tools\server.ps1 stop
```

Ele só mexe nos processos do Godot iniciados **desta** pasta, e **recusa parar ou reiniciar com jogador conectado** (a
posição só é salva no logout; peça para sair pelo menu do jogo, ou use `-Force`). Se não achar o Godot, passe
`-GodotPath "<caminho do Godot_v4.7.x-stable_mono_win64_console.exe>"` ou defina a variável de ambiente `GODOT_PATH`.

Na mão, é o mesmo que:

```powershell
& "<caminho do Godot_v4.7.x-stable_mono_win64_console.exe>" --headless --path "<caminho do SphereEmu-BR>"
```

O servidor está pronto quando o arquivo mais novo em `logs/` (`server_<data>.log`) contém
`Server up, waiting for connections...`. Ele escuta na porta `25860`. A primeira partida leva alguns segundos.

Para parar, encerre esse processo. Antes de reiniciar com alguém conectado, peça para a pessoa **sair pelo menu do
jogo** (logout): a posição só é salva no logout.

## 4. Aponte o cliente do jogo para o seu servidor

Na pasta onde o cliente está instalado, edite `connect.cfg` e `connectn.cfg` e coloque:

```
MAIN_URL "127.0.0.1"
PORT 25860
```

Use `127.0.0.1` quando o servidor roda no mesmo computador. Para o servidor de um amigo, use o endereço do
computador dele. Guarde uma cópia dos arquivos originais, para poder voltar ao servidor oficial.

Inicie o cliente com o argumento `/login`, usando o `sphereclient_patched.exe` que o passo 2 deixou na pasta do
repositório (copie para a pasta do jogo, ao lado do `sphereclient.exe`):

```
sphereclient_patched.exe /login
```

Nunca compartilhe o seu `connect.cfg`: ele guarda o seu login de forma ofuscada.

## 5. Crie sua conta e jogue

Na tela de login digite qualquer login e senha. **O primeiro login cria a conta.** Escolha uma senha que você não
use em nenhum outro lugar e guarde-a: ainda não existe recuperação. Depois crie um personagem e entre no mundo.

Senhas erradas têm limite: depois de 5 erros em 5 minutos aquele login fica bloqueado por um tempo.

## 6. Virar GM (só no seu próprio servidor)

Os comandos de GM são para teste. Só funcionam com `DebugMode` igual a `true` no `appsettings.json`.

1. Abra o `appsettings.json` na pasta do repositório.
2. Coloque o seu login em `GmAccounts` (vários logins separados por vírgula):
   ```json
   "GmAccounts": "seulogin",
   ```
3. Se a conta **ainda não existe**, defina também `"GmAccountBootstrap": "true"`, inicie o servidor e entre uma vez
   com esse login para criá-la. Depois volte `GmAccountBootstrap` para `"false"` e reinicie o servidor. Se a conta já existe, não precisa do bootstrap.
4. No jogo, digite os comandos na caixa de chat, começando com `/`.

Por que existe o bootstrap: sem ele, num banco novo, a primeira pessoa que entrasse com um nome de GM viraria GM.
Num servidor compartilhado, liste só administradores de confiança e use senhas fortes.

## Comandos de GM mais usados

Digite o comando sem argumentos para ver o uso, quando ele exige algum.

| Comando | O que faz |
|---|---|
| `/lvl <título> <grau>` | Define o seu nível |
| `/gold <valor>` | Define o seu dinheiro |
| `/attr <valor>` ou 8 valores | Define atributos |
| `/stats ...` | Define valores de status |
| `/give <id_do_objeto> [quantidade]` | Cria um item |
| `/giveinv`, `/giveinvns` | Põe um item no inventário (por id, ou por nome com sufixo e tier) |
| `/bag` | Cria uma bolsa |
| `/inv` | Lista o que o servidor guarda para você |
| `/clearinv` | Esvazia o inventário |
| `/mob`, `/mobid` | Cria um monstro |
| `/loot` | Teste de loot |
| `/tp`, `/pos` | Teleporta, mostra a posição |
| `/die` | Mata o seu personagem |
| `/karma`, `/guild`, `/clan` | Define karma, guilda, clã |
| `/buff` | Aplica um buff |
| `/time [day\|night\|HH[:MM]]` | Define a hora do jogo |
| `/safezone`, `/ghost` | Liga ou desliga a proteção de cidade e o modo fantasma |

Todo comando de GM fica registrado em `logs/server_*.log` com o nome do jogador.

## Configurações que vale conhecer

Todas no `appsettings.json`, com comentários no próprio arquivo. As mais usadas: `Port`, `DebugMode`,
`GmAccounts`, `GmAccountBootstrap`, `AllowSelfAttack`, `MobChaseEnabled`, `CityMonsterProtection`, `Spawn_Money`, `RespawnDelaySeconds` (padrão 8,3), `RespawnAtCity` (padrão true) e `DeathExperienceLossPercent` (padrão 0, desligado; no oficial parecia 5,75). Uma chave que você não escrever fica com o padrão.

## Problemas

| Sintoma | O que verificar |
|---|---|
| O cliente não conecta | O servidor está rodando (`Server up` no log), a porta é `25860`, o `MAIN_URL` está certo, nada mais usa a porta |
| O login volta para a tela de login | Senha errada, ou o bloqueio está ativo (espere alguns minutos). O log diz qual |
| Os comandos de GM não fazem nada | `DebugMode` é `true`, seu login está em `GmAccounts`, e a mensagem começa com `/` |
| O servidor não inicia | O `dotnet build` passa, e o passo 2 terminou (as pastas `Godot/Models`, `Sphere.GameDataDecode` e `.generated` existem) |
| Algo estranho no jogo | Abra uma issue com as linhas do log em volta. Veja o [CONTRIBUTING.pt-BR.md](../CONTRIBUTING.pt-BR.md) |

Não compartilhe `sph.db`, logs nem capturas: eles guardam contas e chat.
