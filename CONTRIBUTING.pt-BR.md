# Como contribuir

Languages: [English](CONTRIBUTING.md) | Português | [Italiano](CONTRIBUTING.it.md)

Obrigado por ajudar. Este projeto é um emulador de servidor do Sphere. O objetivo é se comportar como o jogo
original, então toda mudança de comportamento precisa de evidência.

## Branches

| Branch | Para quê | Quem escreve |
|---|---|---|
| `main` | Base estável. Sempre compila e roda. | Mantenedores, só por pull request revisado |
| `develop` | Junção das contribuições aceitas. | Mantenedores |
| `feature/*`, `fix/*` | Seu trabalho, no seu fork. | Você |

Abra os pull requests para `develop`. A `main` recebe o que já foi testado.

## Idioma

- Código, comentários, commits, issues e pull requests: **inglês**. O autor original lê isso.
- Conversa da comunidade, guias e suporte: português.

## Como é uma boa mudança

1. **Um problema por pull request.** Pequeno e focado.
2. **Evidência.** Para qualquer mudança de comportamento, diga de onde ela vem:
   - captura do servidor oficial (log de pacotes, sem dados de conta),
   - dados do cliente (`params`, `language`, módulos `.mbc`),
   - rotina decompilada do cliente,
   - ou "custom": uma diferença deliberada do original, dita como tal e, se for regra de servidor, atrás de uma
     configuração no `appsettings.json`.
3. **Siga o código ao redor.** Respeite o `.editorconfig` e os nomes do arquivo. Não reformate arquivos.
4. **Comentários explicam o porquê**, curtos.
5. **A build passa:** `dotnet build SphServer.sln` sem erros.
6. **Mudança no formato de pacote** é conferida com o esquema do protocolo. Não chute larguras de campo; leia a
   definição do módulo e o caminho do cliente antes (`Sphere.NetworkProtocol/`).

## Não envie

- Arquivos do jogo que não são seus para redistribuir (cliente, modelos, texturas, sons).
- Capturas de pacotes, `sph.db`, logs, ou qualquer coisa com login, senha, hash ou chat.
- Segredos de qualquer tipo.

O `.gitignore` cobre os casos comuns. Na dúvida, pergunte no pull request.

## Usando assistentes de IA

Bem-vindos, com as mesmas regras das pessoas:

- Você responde pelo que envia. Leia o diff antes de abrir o pull request.
- O assistente deve seguir o `AGENTS.md`.
- Diga na descrição o que você rodou e o que viu. "Compila" não é "testei no jogo".
- Não deixe o assistente inventar detalhes de protocolo. Campo desconhecido fica marcado como desconhecido.

## O que precisa de aprovação de mantenedor

Autenticação e contas, comandos de GM e permissões, anti-cheat e validações, esquema do banco, enquadramento e
criptografia de rede. Abra uma issue antes.

## Reportando bugs

Informe: o que você fez, o que esperava, o que aconteceu, as linhas do log do servidor em volta (`logs/server_*.log`)
e o horário. Uma descrição do personagem (nível, itens) ajuda. Remova dados de conta antes.

## Sincronizando com o upstream

Os mantenedores leem o que o projeto original fez pelo remoto dele (veja [docs/UPSTREAM.md](docs/UPSTREAM.md)), portam o que
quiserem em uma branch separada (este repositório não compartilha histórico com ele, então as mudanças são portadas, não
mescladas), compilam, testam e só depois abrem um pull request para `develop`. Correções que não são específicas desta comunidade também devem ir para o upstream como
pull requests.
